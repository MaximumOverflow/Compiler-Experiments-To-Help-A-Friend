using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public sealed class Function
{
	public required bool Public { get; init; }
	public required LLVMValueRef LlvmValue { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }

	public required Type ReturnType { get; init; }
	public required IReadOnlyList<(ReadOnlyMemory<char>, Type)> Parameters;

	internal static void SetBody(FileCompilationContext context, Function function, BlockNode body)
	{
		ClearBody(function.LlvmValue);
		using var builder = context.GlobalContext.LlvmContext.CreateBuilder();
		var block = new Block(body, function, context);
		block.Compile(builder, true);
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