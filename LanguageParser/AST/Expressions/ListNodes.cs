using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class TupleNode: IExpressionNode, IParseableNode<TupleNode>
{
	public required IReadOnlyList<IExpressionNode> Values { get; init; }

	public static bool TryParse(ref TokenStream stream, out TupleNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.OpenRound })
			return false;

		var values = new List<IExpressionNode>();

		if (tokens.Current is { Type: TokenType.CloseRound })
		{
			tokens.MoveNext();
			
			stream = tokens;
			result = new TupleNode { Values = values };
			return true;
		}

		while (true)
		{
			if (!IExpressionNode.TryParse(ref tokens, false, out var value))
				return UnexpectedTokenException.Throw<bool>(tokens.Current);
			
			values.Add(value);

			var nextToken = tokens.MoveNext();
			if(nextToken is { Type: TokenType.CloseRound })
				break;

			if (nextToken is not { Type: TokenType.Comma })
				return UnexpectedTokenException.Throw<bool>(nextToken, TokenType.Comma);
		}

		stream = tokens;
		result = new TupleNode { Values = values };
		return true;
	}
}

internal sealed class ArrayNode: IExpressionNode, IParseableNode<ArrayNode>
{
	public required IReadOnlyList<IExpressionNode> Values { get; init; }

	public static bool TryParse(ref TokenStream stream, out ArrayNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.OpenSquare })
			return false;

		var values = new List<IExpressionNode>();

		if (tokens.Current is { Type: TokenType.CloseSquare })
		{
			tokens.MoveNext();
			
			stream = tokens;
			result = new ArrayNode { Values = values };
			return true;
		}

		while (true)
		{
			if (!IExpressionNode.TryParse(ref tokens, false, out var value))
				return UnexpectedTokenException.Throw<bool>(tokens.Current);
			
			values.Add(value);

			var nextToken = tokens.MoveNext();
			if(nextToken is { Type: TokenType.CloseSquare })
				break;

			if (nextToken is not { Type: TokenType.Comma })
				return UnexpectedTokenException.Throw<bool>(nextToken, TokenType.Comma);
		}

		stream = tokens;
		result = new ArrayNode { Values = values };
		return true;
	}
}