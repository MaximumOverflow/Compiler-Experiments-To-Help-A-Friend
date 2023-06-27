using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class WhileNode : AstNode, IStatementNode, IParseableNode<WhileNode>
{
	public required ExpressionNode Condition { get; init; }
	public required BlockNode Block { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out WhileNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.While })
			return false;

		if (!ExpressionNode.TryParse(ref tokens, false, out var condition))
			return false;

		if (!BlockNode.TryParse(ref tokens, out var block))
			return false;

		stream = tokens;
		result = new WhileNode
		{
			Condition = condition,
			Block = block,
		};

		return true;
	}
}