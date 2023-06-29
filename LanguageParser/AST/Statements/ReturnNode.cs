using LanguageParser.Tokenizer;
using LanguageParser.Parser;
	
namespace LanguageParser.AST;

internal sealed class ReturnNode:  IStatementNode, IParseableNode<ReturnNode>
{
	public required IExpressionNode? Value { get; init; }
	public static bool TryParse(ref TokenStream stream, out ReturnNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.Return })
			return false;

		IExpressionNode? value = null;
		if (tokens.Current is { Type: TokenType.Semicolon }) tokens.MoveNext();
		else if (!IExpressionNode.TryParse(ref tokens, false, out value))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		stream = tokens;
		result = new ReturnNode { Value = value };
		return true;
	}
}