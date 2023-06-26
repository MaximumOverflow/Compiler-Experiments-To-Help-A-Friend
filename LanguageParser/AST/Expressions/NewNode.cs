using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class NewNode : ExpressionNode, IParameterizedParseableNode<NewNode, int>
{
	public required TypeNode Type { get; init; }
	public required IReadOnlyList<(ReadOnlyMemory<char>, ExpressionNode)> MemberAssignments { get; init; }

	public static bool TryParse(ref TokenStream stream, int recursionChances, out NewNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (recursionChances == 0) 
			return false;

		if (tokens.MoveNext() is not { Type: TokenType.New })
			return false;

		if (!TypeNode.TryParse(ref tokens, out var type))
			throw new UnexpectedTokenException(tokens.Current ?? throw new EndOfStreamException());

		if (!tokens.ExpectToken(TokenType.OpeningBracket))
			return false;

		var memberAssignments = new List<(ReadOnlyMemory<char>, ExpressionNode)>();
		while (true)
		{
			var exit = false;
			switch (tokens.MoveNext())
			{
				case {Type: TokenType.Name} token:
				{
					if (!tokens.ExpectToken(TokenType.AssignmentSeparator))
						return false;

					if (!ExpressionNode.TryParse(ref tokens, new(false, recursionChances - 1), out var value))
						throw new UnexpectedTokenException(tokens.Current ?? throw new EndOfStreamException());
					
					if (!tokens.ExpectToken(TokenType.Comma))
						return false;
					
					memberAssignments.Add((token.Text, value));
					
					break;
				}
				
				case {Type: TokenType.ClosingBracket}:
					exit = true;
					break;
					
				case null: throw new EndOfStreamException();
				case {} token: throw new UnexpectedTokenException(token);
			}
			
			if(exit)
				break;
		}

		stream = tokens;
		result = new NewNode
		{
			Type = type,
			MemberAssignments = memberAssignments,
		};
		return true;
	}
}