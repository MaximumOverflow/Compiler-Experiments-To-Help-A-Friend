namespace Squyrm.Parser.AST;

public sealed class BinaryExpressionNode : IExpressionNode, IParseableNode<BinaryExpressionNode>
{
	public BinaryOperation Operation { get; }
	public IExpressionNode Left { get; }
	public IAstNode Right { get; }

	internal BinaryExpressionNode(IExpressionNode left, IAstNode right, BinaryOperation operation)
	{
		Left = left;
		Right = right;
		Operation = operation;
	}

	public static bool TryParse(ref TokenStream stream,  out BinaryExpressionNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (!tokens.Valid) 
			return false;
		
		if(!TryParseOperand(ref tokens, out var left))
			return false;

		if (!TryParseOperator(ref tokens, out var operation))
		{
			if (left is not BinaryExpressionNode binResult) 
				return false;

			stream = tokens;
			result = binResult;
			return true;
		}

		if(!TryParseOperand(ref tokens, out var right))
			return false;

		while (true)
		{
			if (!TryParseOperator(ref tokens, out var otherOperation))
			{
				stream = tokens;
				result = new BinaryExpressionNode((IExpressionNode) left, right, operation);
				return true;
			}
			
			if(!TryParseOperand(ref tokens, out var other))
				return UnexpectedTokenException.Throw<bool>(tokens.Current);

			if (IsArithmeticWithPrecedence(operation))
			{
				left = new BinaryExpressionNode((IExpressionNode) left, right, operation);
				operation = otherOperation;
				right = other;
			}
			else
			{
				right = new BinaryExpressionNode((IExpressionNode) right, other, otherOperation);
			}
		}
	}

	private static bool TryParseOperand(ref TokenStream stream, out IAstNode result)
	{
		if (TryParseAccessChain(ref stream, out var chain))
		{
			result = chain;
			return true;
		}

		if (IExpressionNode.TryParse(ref stream, true, out var expr))
		{
			result = expr;
			return true;
		}

		result = default!;
		return false;
	}

	public static bool TryParseAccessChain(ref TokenStream stream, out BinaryExpressionNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (!IExpressionNode.TryParse(ref tokens, true, out var left))
			return false;

		if (!TryParseOperator(ref tokens, out var operation))
			return false;

		if (operation != BinaryOperation.Access)
			return false;
		
		if (!IExpressionNode.TryParse(ref tokens, true, out var right))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		var loop = true;
		while (loop)
		{
			if (!TryParseOperator(ref tokens, out var otherOperation))
				break;

			switch (otherOperation)
			{
				case BinaryOperation.Access:
				{
					if (!IExpressionNode.TryParse(ref tokens, true, out var other))
						UnexpectedTokenException.Throw<bool>(tokens.Current);

					left = new BinaryExpressionNode(left, right, operation);
					operation = otherOperation;
					right = other;
					break;
				}

				case BinaryOperation.Call:
				{
					if (!RoundBracketedValueList.TryParse(ref tokens, out var other))
						UnexpectedTokenException.Throw<bool>(tokens.Current);

					left = new BinaryExpressionNode(left, right, operation);
					operation = otherOperation;
					right = other;
					break;
				}
				
				case BinaryOperation.Indexing:
				{
					if (!SquareBracketedValueList.TryParse(ref tokens, out var other))
						UnexpectedTokenException.Throw<bool>(tokens.Current);

					left = new BinaryExpressionNode(left, right, operation);
					operation = otherOperation;
					right = other;
					break;
				}

				default:
					tokens.Position--;
					loop = false;
					break;
			}
		}

		stream = tokens;
		result = new BinaryExpressionNode(left, right, operation);
		return true;
	}

	private static bool TryParseOperator(ref TokenStream stream, out BinaryOperation operation)
	{
		BinaryOperation? op = stream.Current switch
		{
			{ Type: TokenType.Period } => BinaryOperation.Access,
			{ Type: TokenType.Addition } => BinaryOperation.Addition,
			{ Type: TokenType.Division } => BinaryOperation.Division,
			{ Type: TokenType.Exponential } => BinaryOperation.Exponential,
			{ Type: TokenType.Subtraction } => BinaryOperation.Subtraction,
			{ Type: TokenType.Multiplication } => BinaryOperation.Multiplication,
			{ Type: TokenType.Modulo } => BinaryOperation.Modulo,
			{ Type: TokenType.Range } => BinaryOperation.Range,
			{ Type: TokenType.Equal } => BinaryOperation.CmpEq,
			{ Type: TokenType.NotEqual } => BinaryOperation.CmpNe,
			{ Type: TokenType.LargerThan } => BinaryOperation.CmpGt,
			{ Type: TokenType.LessThan } => BinaryOperation.CmpLt,
			{ Type: TokenType.LargerThanOrEqual } => BinaryOperation.CmpGe,
			{ Type: TokenType.LessThanOrEqual } => BinaryOperation.CmpLe,
			{ Type: TokenType.OpenRound } => BinaryOperation.Call,
			{ Type: TokenType.OpenSquare } => BinaryOperation.Indexing,
			{ Type: TokenType.AssignmentSeparator } => BinaryOperation.Assign,
			{ Type: TokenType.As } => BinaryOperation.Cast,
			_ => null,
		};

		switch (op)
		{
			case BinaryOperation.Call or BinaryOperation.Indexing:
				operation = op.Value;
				return true;
			
			case not null:
				stream.MoveNext();
				operation = op.Value;
				return true;
			
			case null:
				operation = default;
				return false;
		}
	}

	private static bool IsSpecialOperation(BinaryOperation operation) => operation switch
	{
		BinaryOperation.Call or
		BinaryOperation.Access or
		BinaryOperation.Indexing => true,
		_ => false,
	};

	private static bool IsArithmeticWithPrecedence(BinaryOperation operation) => operation switch
	{
		BinaryOperation.Modulo or
		BinaryOperation.Division or
		BinaryOperation.Multiplication => true,
		_ => false,
	};
}

public enum BinaryOperation
{
	Addition,
	Subtraction,
	Multiplication,
	Division,
	Modulo,
	Exponential,
	Range,

	Call,
	Access,
	Indexing,

	CmpEq,
	CmpNe,
	CmpGt,
	CmpLt,
	CmpGe,
	CmpLe,
	Assign,
	Cast,
}