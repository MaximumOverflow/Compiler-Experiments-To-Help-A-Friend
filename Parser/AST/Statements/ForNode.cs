namespace Squyrm.Parser.AST;

public sealed class ForNode:  IStatementNode, IParseableNode<ForNode>
{
	public bool RequiresSemicolon => false;
	public ReadOnlyMemory<char> VarName { get; }
	public IExpressionNode Enumerable { get; }
	public BlockNode Block { get; }

	internal ForNode(ReadOnlyMemory<char> varName, IExpressionNode enumerable, BlockNode block)
	{
		VarName = varName;
		Enumerable = enumerable;
		Block = block;
	}

	public static bool TryParse(ref TokenStream stream, out ForNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.For })
			return false;

		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;

		if (!tokens.ExpectToken(TokenType.In))
			return false;
		
		if (!IExpressionNode.TryParse(ref tokens, false, out var enumerable))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		if (!BlockNode.TryParse(ref tokens, out var block))
			return false;

		stream = tokens;
		result = new ForNode(name, enumerable, block);
		return true;
	}
}