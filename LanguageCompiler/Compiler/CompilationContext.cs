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

	private readonly Dictionary<string, LLVMAttributeRef> _attributes;
	private readonly Dictionary<ReadOnlyMemory<char>, Value> _strings;
	internal readonly Dictionary<ReadOnlyMemory<char>, Type> DefaultTypes;
	internal readonly Dictionary<ReadOnlyMemory<char>, Namespace> Namespaces;


	public CompilationContext(CompilationSettings compilationSettings)
	{
		CompilationSettings = compilationSettings;
		
		LlvmContext = LLVMContextRef.Create();
		LlvmModule = LlvmContext.CreateModuleWithName(compilationSettings.ModuleName);

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
	
	public void CompileSourceFile(string source)
	{
		if (_finalized)
			throw new InvalidOperationException("Context has already been finalized.");

		RootNode root;
		var stats = new RuntimeStats();
		{
			var tokens = Tokenizer.Tokenize(source);
			var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));
		
			if (!RootNode.TryParse(ref stream, out root)) 
				throw new Exception("Failed to parse root node.");
			
			stats.Dump("Parsing", ConsoleColor.Blue);
		}
		
		stats = new RuntimeStats();

		if(!Namespaces.TryGetValue(root.Namespace, out var @namespace))
			Namespaces.Add(root.Namespace, @namespace = new Namespace(root.Namespace));
		
		var context = new FileCompilationContext(this, @namespace);
		context.DefineTypes(root.Declarations);
		context.DefineFunctions(root.Declarations);
		
		stats.Dump("IL generation", ConsoleColor.Blue);
	}

	public void FinalizeCompilation()
	{
		_finalized = true;
		FinalizeReflectionInformation();
		LlvmModule.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);
		
		unsafe
		{
			var stats = new RuntimeStats();
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
				functionPassManager.RunFunctionPassManager(fn);

			modulePassManager.Run(LlvmModule);
			stats.Dump("All optimization passes", ConsoleColor.Blue);
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
		
		var i8Ptr = DefaultTypes["i8".AsMemory()].MakePointer();
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