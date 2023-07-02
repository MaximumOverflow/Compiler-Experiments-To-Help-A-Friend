namespace Squyrm.Parser.AST;

public sealed class UnaryOperationNode : IExpressionNode, IParseableNode<UnaryOperationNode>
{
	public IAstNode Expression { get; }
	public UnaryOperationType Operation { get; }

	internal UnaryOperationNode(IAstNode expression, UnaryOperationType operation)
	{
		Expression = expression;
		Operation = operation;
	}

	public static bool TryParse(ref TokenStream stream, out UnaryOperationNode result)
	{
		result = default!;
		var tokens = stream;

		switch (tokens.MoveNext())
		{
			case { Type: TokenType.Subtraction }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				result = new UnaryOperationNode(value, UnaryOperationType.Negate);
				break;
			}
			
			case { Type: TokenType.And }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				result = new UnaryOperationNode(value, UnaryOperationType.AddrOf);
				break;
			}
			
			case { Type: TokenType.Multiplication }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				result = new UnaryOperationNode(value, UnaryOperationType.ValueOf);
				break;
			}

			case { Type: TokenType.TypeId }:
			{
				tokens.ExpectToken(TokenType.OpenRound);
				
				if (!TypeNode.TryParse(ref tokens, out var type))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				tokens.ExpectToken(TokenType.CloseRound);
				
				result = new UnaryOperationNode(type, UnaryOperationType.TypeId);
				break;
			}

			case { Type: TokenType.Undefined }:
			{
				if (!TypeNode.TryParse(ref tokens, out var type))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
				
				result = new UnaryOperationNode(type, UnaryOperationType.Undefined);
				break;
			}
				
			default: return false;
		}
		
		stream = tokens;
		return true;
	}
}

public enum UnaryOperationType
{
	Negate,
	AddrOf,
	ValueOf,
	Undefined,
	TypeId
}