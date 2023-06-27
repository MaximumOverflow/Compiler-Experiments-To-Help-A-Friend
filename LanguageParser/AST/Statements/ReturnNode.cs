using LanguageParser.Tokenizer;
using LanguageParser.Parser;
	
namespace LanguageParser.AST;

internal sealed class ReturnNode : AstNode, IStatementNode, IParseableNode<ReturnNode>
{
	public required ExpressionNode? Value { get; init; }
	public static bool TryParse(ref TokenStream stream, out ReturnNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.Return })
			return false;

		ExpressionNode? value = null;
		if (tokens.Current is { Type: TokenType.Semicolon })
		{
			tokens.MoveNext();
		}
		else
		{
			if (!ExpressionNode.TryParse(ref tokens, false, out value))
				return false;

			if (!tokens.ExpectToken(TokenType.Semicolon))
				return false;
		}

		stream = tokens;
		result = new ReturnNode { Value = value };
		return true;
	}
}