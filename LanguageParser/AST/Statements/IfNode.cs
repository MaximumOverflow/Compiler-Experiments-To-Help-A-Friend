namespace Squyrm.Parser.AST;

public sealed class IfNode:  IStatementNode, IParseableNode<IfNode>
{
	public bool RequiresSemicolon => false;
	public IExpressionNode Condition { get; }
	public BlockNode Then { get; }
	public IStatementNode? Else { get; }

	internal IfNode(IExpressionNode condition, BlockNode then, IStatementNode? @else)
	{
		Condition = condition;
		Then = then;
		Else = @else;
	}

	public static bool TryParse(ref TokenStream stream, out IfNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.If })
			return false;
		
		if (!IExpressionNode.TryParse(ref tokens, false, out var condition))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		if (!BlockNode.TryParse(ref tokens, out var block))
			return false;

		IStatementNode? @else = null;
		if (tokens.Current is { Type: TokenType.Else })
		{
			tokens.MoveNext();
			if (BlockNode.TryParse(ref tokens, out var elseBlock))
				@else = elseBlock;
			else if (TryParse(ref tokens, out var elseIf))
				@else = elseIf;
		}

		stream = tokens;
		result = new IfNode(condition, block, @else);
		return true;
	}
}