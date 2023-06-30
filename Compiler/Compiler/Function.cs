namespace Squyrm.Compiler;

public sealed class Function
{
	public bool External { get; private set; }
	public bool Public { get; }
	public FunctionType Type { get; }
	public LLVMValueRef LlvmValue { get; }
	public ReadOnlyMemory<char> Name { get; }
	public IReadOnlyList<ReadOnlyMemory<char>> ParameterNames { get; }

	public Function(
		CompilationContext context,
		ReadOnlyMemory<char> name, bool @public, 
		FunctionType type, IReadOnlyList<ReadOnlyMemory<char>> parameterNames
	)
	{
		Type = type;
		Name = name;
		External = true;
		Public = @public;
		ParameterNames = parameterNames;
		LlvmValue = context.LlvmModule.AddFunction(name.Span, type);
	}

	internal static void SetBody(FileCompilationContext context, Function function, BlockNode body)
	{
		ClearBody(function.LlvmValue);
		using var builder = context.GlobalContext.LlvmContext.CreateBuilder();
		var block = new Block(body, function, context);
		
		var (value, type) = block.Compile(builder, out var hasReturned);
		if (!hasReturned && type.LlvmType != LLVMTypeRef.Void)
		{
			var retT = function.Type.ReturnType;
			
			if (type != retT)
				throw new InvalidOperationException($"Expected value of type '{retT.Name}' found '{type.Name}'.");
			
			builder.BuildRet(value);
		}

		function.External = false;
	}

	internal static unsafe void ClearBody(LLVMValueRef function)
	{
		if(function.BasicBlocksCount == 0)
			return;

		foreach (var block in function.BasicBlocks)
			LLVM.RemoveBasicBlockFromParent(block);
	}

	public static implicit operator LLVMValueRef(Function f) => f.LlvmValue;
}