using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class UnaryOperationNode : IExpressionNode, IParseableNode<UnaryOperationNode>
{
	public required IAstNode Expression { get; init; }
	public required UnaryOperationType Operation { get; init; }

	public static bool TryParse(ref TokenStream stream, out UnaryOperationNode result)
	{
		result = default!;
		var tokens = stream;

		switch (tokens.MoveNext())
		{
			case { Type: TokenType.OpenRound }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
		
				if (tokens.MoveNext() is not {Type: TokenType.CloseRound})
					return false;

				result = new UnaryOperationNode
				{
					Expression = value, 
					Operation = UnaryOperationType.Group,
				};
				break;
			}

			case { Type: TokenType.Subtraction }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				result = new UnaryOperationNode
				{
					Expression = value, 
					Operation = UnaryOperationType.Negate,
				};
				break;
			}
			
			case { Type: TokenType.And }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				result = new UnaryOperationNode
				{
					Expression = value, 
					Operation = UnaryOperationType.AddrOf,
				};
				break;
			}
			
			case { Type: TokenType.Multiplication }:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var value))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				result = new UnaryOperationNode
				{
					Expression = value, 
					Operation = UnaryOperationType.ValueOf,
				};
				break;
			}

			case { Type: TokenType.TypeId }:
			{
				tokens.ExpectToken(TokenType.OpenRound);
				
				if (!TypeNode.TryParse(ref tokens, out var type))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);

				tokens.ExpectToken(TokenType.CloseRound);
				
				result = new UnaryOperationNode
				{
					Expression = type, 
					Operation = UnaryOperationType.TypeId,
				};
				break;
			}

			case { Type: TokenType.Undefined }:
			{
				if (!TypeNode.TryParse(ref tokens, out var type))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
				
				result = new UnaryOperationNode
				{
					Expression = type, 
					Operation = UnaryOperationType.Undefined,
				};
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
	Group,
	Negate,
	AddrOf,
	ValueOf,
	Undefined,
	TypeId
}