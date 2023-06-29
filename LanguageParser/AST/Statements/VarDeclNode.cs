using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class VarDeclNode:  IStatementNode, IParseableNode<VarDeclNode>
{
	public bool RequiresSemicolon => true;
	
	public required bool Constant { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required IExpressionNode Value { get; init; }

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
		result = new VarDeclNode
		{
			Name = name, 
			Value = value,
			Constant = firstToken.Type == TokenType.Const,
		};
		return true;
	}
}