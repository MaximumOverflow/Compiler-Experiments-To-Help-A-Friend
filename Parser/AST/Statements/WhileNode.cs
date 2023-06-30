namespace Squyrm.Parser.AST;

public sealed class WhileNode:  IStatementNode, IParseableNode<WhileNode>
{
	public bool RequiresSemicolon => false;
	public IExpressionNode Condition { get; }
	public BlockNode Block { get; }

	internal WhileNode(IExpressionNode condition, BlockNode block)
	{
		Condition = condition;
		Block = block;
	}

	public static bool TryParse(ref TokenStream stream, out WhileNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.While })
			return false;

		if (!IExpressionNode.TryParse(ref tokens, false, out var condition))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		if (!BlockNode.TryParse(ref tokens, out var block))
			return false;

		stream = tokens;
		result = new WhileNode(condition, block);
		return true;
	}
}