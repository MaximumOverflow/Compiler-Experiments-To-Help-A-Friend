using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class BinaryOperationNode : ExpressionNode, IParseableNode<BinaryOperationNode>
{
	public OperationType Operation { get; }
	public ExpressionNode Left { get; }
	public ExpressionNode Right { get; }

	public BinaryOperationNode(ExpressionNode left, ExpressionNode right, OperationType operation)
	{
		Left = left;
		Right = right;
		Operation = operation;
	}

	public static bool TryParse(ref TokenStream stream,  out BinaryOperationNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (!tokens.Valid) 
			return false;

		if (!ExpressionNode.TryParse(ref tokens, true, out var left))
			throw new UnexpectedTokenException(tokens.Current!.Value);

		OperationType operation;
		switch (tokens.MoveNext())
		{
			case { Type: TokenType.Period }: operation = OperationType.Access; break;
			case { Type: TokenType.Addition }: operation = OperationType.Addition; break;
			case { Type: TokenType.Division }: operation = OperationType.Division; break;
			case { Type: TokenType.Exponential }: operation = OperationType.Exponential; break;
			case { Type: TokenType.Subtraction }: operation = OperationType.Subtraction; break;
			case { Type: TokenType.Multiplication }: operation = OperationType.Multiplication; break;
			case { Type: TokenType.Modulo }: operation = OperationType.Modulo; break;
			case { Type: TokenType.Range }: operation = OperationType.Range; break;
			case { Type: TokenType.Equal }: operation = OperationType.CmpEq; break;
			case { Type: TokenType.NotEqual }: operation = OperationType.CmpNe; break;
			case { Type: TokenType.LargerThan }: operation = OperationType.CmpGt; break;
			case { Type: TokenType.LessThan }: operation = OperationType.CmpLt; break;
			case { Type: TokenType.LargerThanOrEqual }: operation = OperationType.CmpGe; break;
			case { Type: TokenType.LessThanOrEqual }: operation = OperationType.CmpLe; break;
			default: return false;
		}

		if (!ExpressionNode.TryParse(ref tokens, false, out var right))
			throw new UnexpectedTokenException(tokens.Current!.Value);

		if (right is BinaryOperationNode bin && GetPriority(operation) > GetPriority(bin.Operation))
		{
			right = bin.Right;
			left = new BinaryOperationNode(left, bin.Left, operation);
			operation = bin.Operation;
		}

		stream = tokens;
		result = new BinaryOperationNode(left, right, operation);
		return true;
	}

	private static int GetPriority(OperationType operation) => operation switch
	{
		OperationType.Access => 2,
		OperationType.Modulo => 1,
		OperationType.Division => 1,
		OperationType.Multiplication => 1,
		_ => 0,
	};
}

public enum OperationType
{
	Addition,
	Subtraction,
	Multiplication,
	Division,
	Modulo,
	Exponential,
	Range,
	
	Access,
	
	CmpEq,
	CmpNe,
	CmpGt,
	CmpLt,
	CmpGe,
	CmpLe,
}