using System.Diagnostics.CodeAnalysis;
using Squyrm.Compiler.Compiler.Passes;
using System.Text.RegularExpressions;
using Squyrm.Compiler.Passes;
using System.Text;

namespace Squyrm.Compiler;

public sealed partial class CompilationContext : IDisposable
{
	internal LLVMModuleRef LlvmModule;
	internal LLVMContextRef LlvmContext;
	public readonly CompilationSettings CompilationSettings;

	private readonly IEnumerable<string> _files;
	private readonly Dictionary<string, LLVMAttributeRef> _attributes;
	private readonly Dictionary<ReadOnlyMemory<char>, Value> _strings;
	internal readonly Dictionary<ReadOnlyMemory<char>, Type> DefaultTypes;
	internal readonly Dictionary<ReadOnlyMemory<char>, Namespace> Namespaces;
	internal readonly ReflectionGenerationPass.ReflectionInfo? ReflectionInfo;

	public CompilationContext(CompilationSettings compilationSettings, IEnumerable<string> files)
	{
		CompilationSettings = compilationSettings;
		
		LlvmContext = LLVMContextRef.Create();
		LlvmModule = LlvmContext.CreateModuleWithName(compilationSettings.ModuleName);
		LlvmModule.DataLayout = "e";
		
		if(compilationSettings.EmitReflectionInformation)
			ReflectionInfo = new ReflectionGenerationPass.ReflectionInfo();

		_files = files;
		_attributes = new Dictionary<string, LLVMAttributeRef>();
		_strings = new Dictionary<ReadOnlyMemory<char>, Value>(MemoryStringComparer.Instance);
		Namespaces = new Dictionary<ReadOnlyMemory<char>, Namespace>(MemoryStringComparer.Instance);
		DefaultTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance)
		{
			{ "u8".AsMemory(), new IntegerType(LlvmContext, 8, true, ReflectionInfo) },
			{ "i8".AsMemory(), new IntegerType(LlvmContext, 8, false, ReflectionInfo) },
			{ "u16".AsMemory(), new IntegerType(LlvmContext, 16, true, ReflectionInfo) },
			{ "i16".AsMemory(), new IntegerType(LlvmContext, 16, false, ReflectionInfo) },
			{ "u32".AsMemory(), new IntegerType(LlvmContext, 32, true, ReflectionInfo) },
			{ "i32".AsMemory(), new IntegerType(LlvmContext, 32, false, ReflectionInfo) },
			{ "u64".AsMemory(), new IntegerType(LlvmContext, 64, true, ReflectionInfo) },
			{ "i64".AsMemory(), new IntegerType(LlvmContext, 64, false, ReflectionInfo) },
			{ "f32".AsMemory(), new IntrinsicType("f32", LlvmContext.FloatType, ReflectionInfo) },
			{ "f64".AsMemory(), new IntrinsicType("f64", LlvmContext.DoubleType, ReflectionInfo) },
			{ "f128".AsMemory(), new IntrinsicType("f128", LlvmContext.FP128Type, ReflectionInfo) },
			{ "maybe".AsMemory(), new IntrinsicType("maybe", LlvmContext.Int1Type, ReflectionInfo) },
			{ "nothing".AsMemory(), new IntrinsicType("nothing", LlvmContext.VoidType, ReflectionInfo) },
		};
		DefaultTypes.Add("rope".AsMemory(), DefaultTypes["i8".AsMemory()].MakePointer(true));
	}

	public LLVMModuleRef Compile()
	{
		var fileRoots = SourceParsingPass.Execute(_files);
		var translationUnits = SymbolDeclarationPass.Execute(this, fileRoots);
		var reflectionData = ReflectionGenerationPass.Initialize(this);
		
		SymbolCompilationPass.Execute(fileRoots, translationUnits);
		ReflectionGenerationPass.Execute(this,reflectionData);
		
		if (!LlvmModule.TryVerify(LLVMVerifierFailureAction.LLVMPrintMessageAction, out var err))
		{
			LlvmModule.Dump();
			throw new CompilationException(err);
		}
		
		IROptimizationPass.Execute(this, CompilationSettings.OptimizationLevel);
		return LlvmModule;
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