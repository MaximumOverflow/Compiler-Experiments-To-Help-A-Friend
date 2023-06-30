namespace Squyrm.Parser.AST;

public sealed class NewNode : IExpressionNode, IParseableNode<NewNode>
{
	public TypeNode Type { get; }
	public IReadOnlyList<(ReadOnlyMemory<char>, IExpressionNode)> MemberAssignments { get; }

	internal NewNode(TypeNode type, IReadOnlyList<(ReadOnlyMemory<char>, IExpressionNode)> memberAssignments)
	{
		Type = type;
		MemberAssignments = memberAssignments;
	}

	public static bool TryParse(ref TokenStream stream, out NewNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.New })
			return false;

		if (!TypeNode.TryParse(ref tokens, out var type))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		if (!tokens.ExpectToken(TokenType.OpenCurly))
			return false;

		var memberAssignments = new List<(ReadOnlyMemory<char>, IExpressionNode)>();
		while (true)
		{
			var exit = false;
			switch (tokens.MoveNext())
			{
				case {Type: TokenType.Name} token:
				{
					if (!tokens.ExpectToken(TokenType.AssignmentSeparator))
						return false;

					if (!IExpressionNode.TryParse(ref tokens, false, out var value))
						return UnexpectedTokenException.Throw<bool>(tokens.Current);

					memberAssignments.Add((token.Text, value));

					switch (tokens.MoveNext())
					{
						case {Type: TokenType.Comma}:
							break;
						
						case {Type: TokenType.CloseCurly}:
							exit = true;
							break;
						
						case null:
							throw new EndOfStreamException();
						
						case {} unexpected:
							return UnexpectedTokenException.Throw<bool>(unexpected);
					}
					
					break;
				}
				
				case {Type: TokenType.CloseCurly}:
					exit = true;
					break;
					
				case null: throw new EndOfStreamException();
				case {} token: return UnexpectedTokenException.Throw<bool>(token);
			}
			
			if(exit)
				break;
		}

		stream = tokens;
		result = new NewNode(type, memberAssignments);
		return true;
	}
}