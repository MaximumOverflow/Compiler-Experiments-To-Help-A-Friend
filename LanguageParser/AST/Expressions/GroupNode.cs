using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class GroupNode : ExpressionNode, IParseableNode<GroupNode>
{
	public required ExpressionNode Expression { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out GroupNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.OpeningParentheses })
			return false;

		if (!ExpressionNode.TryParse(ref tokens, false, out var expr))
			return false;
		
		if (!tokens.ExpectToken(TokenType.ClosingParentheses))
			return false;
		
		stream = tokens;
		result = new GroupNode { Expression = expr };
		return true;
	}
}