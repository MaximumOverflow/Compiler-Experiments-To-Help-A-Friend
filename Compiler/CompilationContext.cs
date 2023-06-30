using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Text;

namespace Squyrm.Compiler;

public sealed partial class CompilationContext : IDisposable
{
	private bool _finalized;
	public LLVMModuleRef LlvmModule;
	public LLVMContextRef LlvmContext;
	public readonly CompilationSettings CompilationSettings;

	private readonly Dictionary<string, RootNode> _files;
	private readonly Dictionary<string, LLVMAttributeRef> _attributes;
	private readonly Dictionary<ReadOnlyMemory<char>, Value> _strings;
	internal readonly Dictionary<ReadOnlyMemory<char>, Type> DefaultTypes;
	internal readonly Dictionary<ReadOnlyMemory<char>, Namespace> Namespaces;

	public CompilationContext(CompilationSettings compilationSettings)
	{
		CompilationSettings = compilationSettings;
		
		LlvmContext = LLVMContextRef.Create();
		LlvmModule = LlvmContext.CreateModuleWithName(compilationSettings.ModuleName);
		LlvmModule.DataLayout = "e";

		_files = new Dictionary<string, RootNode>();
		_attributes = new Dictionary<string, LLVMAttributeRef>();
		_strings = new Dictionary<ReadOnlyMemory<char>, Value>(MemoryStringComparer.Instance);
		Namespaces = new Dictionary<ReadOnlyMemory<char>, Namespace>(MemoryStringComparer.Instance);
		DefaultTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance)
		{
			{ "u8".AsMemory(), new IntegerType(this, 8, true) },
			{ "i8".AsMemory(), new IntegerType(this, 8, false) },
			{ "u16".AsMemory(), new IntegerType(this, 16, true) },
			{ "i16".AsMemory(), new IntegerType(this, 16, false) },
			{ "u32".AsMemory(), new IntegerType(this, 32, true) },
			{ "i32".AsMemory(), new IntegerType(this, 32, false) },
			{ "u64".AsMemory(), new IntegerType(this, 64, true) },
			{ "i64".AsMemory(), new IntegerType(this, 64, false) },
			{ "f32".AsMemory(), new IntrinsicType(this, "f32", LlvmContext.FloatType) },
			{ "f64".AsMemory(), new IntrinsicType(this, "f64", LlvmContext.DoubleType) },
			{ "f128".AsMemory(), new IntrinsicType(this, "f128", LlvmContext.FP128Type) },
			{ "maybe".AsMemory(), new IntrinsicType(this, "maybe", LlvmContext.Int1Type) },
			{ "nothing".AsMemory(), new IntrinsicType(this, "nothing", LlvmContext.VoidType) },
		};
		DefaultTypes.Add("rope".AsMemory(), DefaultTypes["i8".AsMemory()].MakePointer(true));

		InitializeReflectionInformation();
	}

	public RootNode? ParseSourceFile(string path)
	{
		if (_files.TryGetValue(path, out var root))
			return root;
		
		var code = File.ReadAllText(path);
		var tokens = Tokenizer.Tokenize(code);
		var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));

		bool result;
		try
		{
			result = RootNode.TryParse(ref stream, out root);
		}
		catch (Exception e)
		{
			throw new CompilationException($"Failed to parse file '{path}'.", e);
		}

		if (!result)
			return null;

		_files.Add(path, root);
		return root;
	}

	public void CompileParsedFiles()
	{
		var fileCompilationContexts = new List<FileCompilationContext>(_files.Count);
		foreach (var (path, root) in _files)
		{
			if(!Namespaces.TryGetValue(root.Namespace, out var ns))
				Namespaces.Add(root.Namespace, ns = new Namespace(root.Namespace));

			try
			{
				var context = new FileCompilationContext(this, ns, path);
				context.DeclareSymbols(root.Declarations);
				fileCompilationContexts.Add(context);
			}
			catch (Exception e)
			{
				throw new CompilationException($"Failed to compile file '{path}'.", e);
			}
		}

		foreach (var context in fileCompilationContexts)
		{
			try
			{
				context.CompileSymbols();
			}
			catch (Exception e)
			{
				throw new CompilationException($"Failed to compile file '{context.FilePath}'.", e);
			}
		}
	}
	
	public void CompileSourceCode(string code)
	{
		if (_finalized)
			throw new InvalidOperationException("Context has already been finalized.");

		var tokens = Tokenizer.Tokenize(code);
		var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));
	
		if (!RootNode.TryParse(ref stream, out var root)) 
			throw new Exception("Failed to parse root node.");
		
		if(!Namespaces.TryGetValue(root.Namespace, out var @namespace))
			Namespaces.Add(root.Namespace, @namespace = new Namespace(root.Namespace));
		
		var context = new FileCompilationContext(this, @namespace);
		context.DeclareSymbols(root.Declarations);
		context.CompileSymbols();
	}

	public void FinalizeCompilation()
	{
		_finalized = true;
		FinalizeReflectionInformation();
		if (!LlvmModule.TryVerify(LLVMVerifierFailureAction.LLVMPrintMessageAction, out var err))
		{
			LlvmModule.Dump();
			throw new CompilationException(err);
		}
		
		unsafe
		{
			using LLVMPassManagerBuilderRef passManagerBuilder = LLVM.PassManagerBuilderCreate();
			LLVM.PassManagerBuilderSetOptLevel(passManagerBuilder, CompilationSettings.OptimizationLevel);
			
			using LLVMPassManagerRef modulePassManager = LLVM.CreatePassManager();
			passManagerBuilder.PopulateModulePassManager(modulePassManager);
			
			using var functionPassManager = LlvmModule.CreateFunctionPassManager();
			passManagerBuilder.PopulateFunctionPassManager(functionPassManager);
			functionPassManager.InitializeFunctionPassManager();
			
			modulePassManager.Run(LlvmModule);
		
			foreach (var (_, ns) in Namespaces)
			foreach (var (_, fn) in ns.Functions)
			{
				if (fn.External) continue;
				functionPassManager.RunFunctionPassManager(fn);
			}
		
			modulePassManager.Run(LlvmModule);
		}
	}

	internal unsafe Value MakeConstString(
		ReadOnlyMemory<char> str, 
		LLVMUnnamedAddr unnamedAddr = LLVMUnnamedAddr.LLVMNoUnnamedAddr
	)
	{
		if (_strings.TryGetValue(str, out var value))
			return value;

		var unescaped = Regex.Unescape(str.ToString());
		var constStr = LlvmContext.GetConstString(unescaped, false);
		var global = LlvmModule.AddGlobal(constStr.TypeOf, $"__ConstStr{_strings.Count}__");
		LLVM.SetUnnamedAddress(global, unnamedAddr);
		global.Initializer = constStr;
		
		var i8Ptr = DefaultTypes["i8".AsMemory()].MakePointer(true);
		var llvmValue = LLVMValueRef.CreateConstBitCast(global, i8Ptr);
		
		value = new Value(llvmValue, i8Ptr);
		_strings[str] = value;
		return value;
	}

	internal unsafe LLVMAttributeRef MakeFunctionAttribute(string name)
	{
		if (_attributes.TryGetValue(name, out var attribute))
			return attribute;

		Span<byte> str = stackalloc byte[name.Length];
		Encoding.ASCII.GetBytes(name, str);
		
		fixed (byte* attribName = str)
		{
			var id = LLVM.GetEnumAttributeKindForName((sbyte*) attribName, 8);
			attribute = LLVM.CreateEnumAttribute(LlvmContext, id, 1);
		}
		
		_attributes.Add(name, attribute);
		return attribute;
	}

	[SuppressMessage("ReSharper", "PossiblyImpureMethodCallOnReadonlyVariable")]
	public void Dispose()
	{
		LlvmModule.Dispose();
		LlvmContext.Dispose();
		GC.SuppressFinalize(this);
	}
	
	~CompilationContext() => Dispose();
}