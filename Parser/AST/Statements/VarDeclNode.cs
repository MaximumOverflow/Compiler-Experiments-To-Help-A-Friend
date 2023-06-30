namespace Squyrm.Parser.AST;

public sealed class VarDeclNode:  IStatementNode, IParseableNode<VarDeclNode>
{
	public bool RequiresSemicolon => true;
	
	public bool Constant { get; }
	public ReadOnlyMemory<char> Name { get; }
	public IExpressionNode Value { get; }

	internal VarDeclNode(bool constant, ReadOnlyMemory<char> name, IExpressionNode value)
	{
		Constant = constant;
		Name = name;
		Value = value;
	}

	public static bool TryParse(ref TokenStream stream, out VarDeclNode result)
	{
		var tokens = stream;
		result = null!;
		
		if (tokens.MoveNext() is not { Type: TokenType.Var or TokenType.Const } firstToken)
			return false;
		
		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name)) 
			return false;
		
		if (!tokens.ExpectToken(TokenType.AssignmentSeparator)) 
			return false;
		
		if (!IExpressionNode.TryParse(ref tokens, false, out var value))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		stream = tokens;
		result = new VarDeclNode(firstToken.Type == TokenType.Const, name, value);
		return true;
	}
}