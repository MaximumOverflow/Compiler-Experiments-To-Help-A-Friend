using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class IfNode:  IStatementNode, IParseableNode<IfNode>
{
	public bool RequiresSemicolon => false;
	public required IExpressionNode Condition { get; init; }
	public required BlockNode Then { get; init; }
	public required IStatementNode? Else { get; init; }

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
		result = new IfNode
		{
			Condition = condition,
			Then = block,
			Else = @else,
		};

		return true;
	}
}