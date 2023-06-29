using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public sealed class Function
{
	public required bool Public { get; init; }
	public required FunctionType Type { get; init; }
	public required LLVMValueRef LlvmValue { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required IReadOnlyList<ReadOnlyMemory<char>> ParameterNames { get; init; }

	internal static void SetBody(FileCompilationContext context, Function function, BlockNode body)
	{
		ClearBody(function.LlvmValue);
		using var builder = context.GlobalContext.LlvmContext.CreateBuilder();
		var block = new Block(body, function, context);
		
		var (value, type) = block.Compile(builder, true, out var hasReturned);
		if (!hasReturned && type.LlvmType != LLVMTypeRef.Void)
		{
			var retT = function.Type.ReturnType;
			
			if (type != retT)
				throw new InvalidOperationException($"Expected value of type '{retT.Name}' found '{type.Name}'.");
			
			builder.BuildRet(value);
		}
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