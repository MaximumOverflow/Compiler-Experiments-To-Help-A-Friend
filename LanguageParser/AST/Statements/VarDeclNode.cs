using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class VarDeclNode : AstNode, IStatementNode, IParseableNode<VarDeclNode>
{
	public required ReadOnlyMemory<char> Name { get; init; }
	public required ExpressionNode Value { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out VarDeclNode result)
	{
		var tokens = stream;
		result = null!;

		if (tokens.MoveNext() is not { Type: TokenType.Var })
			return false;
		
		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name)) 
			return false;
		
		if (!tokens.ExpectToken(TokenType.AssignmentSeparator)) 
			return false;
		
		if (!ExpressionNode.TryParse(ref tokens, false, out var value))
			throw new UnexpectedTokenException(tokens.Current!.Value);
		
		if (!tokens.ExpectToken(TokenType.Semicolon))
			return false;

		stream = tokens;
		result = new VarDeclNode { Name = name, Value = value };
		return true;
	}
}