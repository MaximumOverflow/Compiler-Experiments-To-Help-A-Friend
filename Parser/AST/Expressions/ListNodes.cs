namespace Squyrm.Parser.AST;

public sealed class RoundBracketedValueList: IExpressionNode, IParseableNode<RoundBracketedValueList>
{
	public IReadOnlyList<IExpressionNode> Values { get; }

	internal RoundBracketedValueList(IReadOnlyList<IExpressionNode> values)
	{
		Values = values;
	}

	public static bool TryParse(ref TokenStream stream, out RoundBracketedValueList result)
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
			result = new RoundBracketedValueList(values);
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
		result = new RoundBracketedValueList(values);
		return true;
	}
}

public sealed class SquareBracketedValueList: IExpressionNode, IParseableNode<SquareBracketedValueList>
{
	public IReadOnlyList<IExpressionNode> Values { get; }

	internal SquareBracketedValueList(IReadOnlyList<IExpressionNode> values)
	{
		Values = values;
	}

	public static bool TryParse(ref TokenStream stream, out SquareBracketedValueList result)
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
			result = new SquareBracketedValueList(values);
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
		result = new SquareBracketedValueList(values);
		return true;
	}
}