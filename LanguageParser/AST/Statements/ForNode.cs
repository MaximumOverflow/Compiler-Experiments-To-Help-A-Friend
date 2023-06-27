using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class ForNode : AstNode, IStatementNode, IParseableNode<ForNode>
{
	public required ReadOnlyMemory<char> Var { get; init; }
	public required ExpressionNode Enumerable { get; init; }
	public required BlockNode Block { get; init; }
	
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
		
		if (!ExpressionNode.TryParse(ref tokens, false, out var enumerable))
			return false;

		if (!BlockNode.TryParse(ref tokens, out var block))
			return false;

		stream = tokens;
		result = new ForNode
		{
			Var = name,
			Enumerable = enumerable,
			Block = block,
		};

		return true;
	}
}