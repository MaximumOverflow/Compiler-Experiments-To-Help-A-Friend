using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LanguageParser.Parser;
using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public sealed partial class CompilationContext : IDisposable
{
	private bool _finalized;
	public LLVMModuleRef LlvmModule;
	public LLVMContextRef LlvmContext;
	public readonly CompilationSettings CompilationSettings;
	
	private readonly Dictionary<ReadOnlyMemory<char>, Value> _strings;
	internal readonly Dictionary<ReadOnlyMemory<char>, Type> DefaultTypes;
	internal readonly Dictionary<ReadOnlyMemory<char>, Namespace> Namespaces;


	public CompilationContext(CompilationSettings compilationSettings)
	{
		CompilationSettings = compilationSettings;
		
		LlvmContext = LLVMContextRef.Create();
		LlvmModule = LlvmContext.CreateModuleWithName(compilationSettings.ModuleName);

		_strings = new Dictionary<ReadOnlyMemory<char>, Value>(MemoryStringComparer.Instance);
		Namespaces = new Dictionary<ReadOnlyMemory<char>, Namespace>(MemoryStringComparer.Instance);
		DefaultTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance)
		{
			{ "i8".AsMemory(), new IntrinsicType(this, "i8", LlvmContext.Int8Type) },
			{ "i16".AsMemory(), new IntrinsicType(this, "i16", LlvmContext.Int16Type) },
			{ "i32".AsMemory(), new IntrinsicType(this, "i32", LlvmContext.Int32Type) },
			{ "i64".AsMemory(), new IntrinsicType(this, "i64", LlvmContext.Int64Type) },
			{ "f32".AsMemory(), new IntrinsicType(this, "f32", LlvmContext.FloatType) },
			{ "f64".AsMemory(), new IntrinsicType(this, "f64", LlvmContext.DoubleType) },
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
		
		var tokens = Tokenizer.Tokenizer.Tokenize(source);
		var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));
		
		if (!RootNode.TryParse(ref stream, out var root)) 
			throw new Exception("Failed to parse root node.");
		
		// Console.WriteLine();
		// Console.WriteLine(((IAstNode) root).GetDebugString("   "));

		if(!Namespaces.TryGetValue(root.Namespace, out var @namespace))
			Namespaces.Add(root.Namespace, @namespace = new Namespace(root.Namespace));
		
		var context = new FileCompilationContext(this, @namespace);
		context.DefineTypes(root.Declarations);
		context.DefineFunctions(root.Declarations);
	}

	public void FinalizeCompilation()
	{
		_finalized = true;
		FinalizeReflectionInformation();
		LlvmModule.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);
		
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

			foreach (var fn in Namespaces.Values.SelectMany(ns => ns.Functions.Values))
				functionPassManager.RunFunctionPassManager(fn);
			
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
		
		var i8Ptr = DefaultTypes["i8".AsMemory()].MakePointer();
		var llvmValue = LLVMValueRef.CreateConstBitCast(global, i8Ptr);
		
		value = new Value(llvmValue, i8Ptr);
		_strings[str] = value;
		return value;
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