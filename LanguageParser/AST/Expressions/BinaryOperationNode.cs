using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class BinaryOperationNode : ExpressionNode, IParameterizedParseableNode<BinaryOperationNode, int>
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

	public static bool TryParse(ref TokenStream stream, int recursionChances, out BinaryOperationNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (!tokens.Valid || recursionChances == 0) 
			return false;

		if (!ExpressionNode.TryParse(ref tokens, new ParsingParams(true, recursionChances - 1), out var left))
			throw new UnexpectedTokenException(tokens.Current!.Value);

		var operation = tokens.MoveNext() switch
		{
			{ Type: TokenType.Addition } => OperationType.Addition,
			{ Type: TokenType.Division } => OperationType.Division,
			{ Type: TokenType.Exponential } => OperationType.Exponential,
			{ Type: TokenType.Subtraction } => OperationType.Subtraction,
			{ Type: TokenType.Multiplication } => OperationType.Multiplication,
			{ Type: TokenType.Modulo } => OperationType.Modulo,
			{ Type: TokenType.Range } => OperationType.Range,
			{ Type: TokenType.Equal } => OperationType.CmpEq,
			{ Type: TokenType.NotEqual } => OperationType.CmpNe,
			{ Type: TokenType.LargerThan } => OperationType.CmpGt,
			{ Type: TokenType.LessThan } => OperationType.CmpLt,
			{ Type: TokenType.LargerThanOrEqual } => OperationType.CmpGe,
			{ Type: TokenType.LessThanOrEqual } => OperationType.CmpLe,
			null => throw new EndOfStreamException(),
			var token => throw new UnexpectedTokenException(token.Value),
		};

		if (!ExpressionNode.TryParse(ref tokens, new ParsingParams(false, recursionChances - 1), out var right))
			throw new UnexpectedTokenException(tokens.Current!.Value);

		stream = tokens;
		result = new BinaryOperationNode(left, right, operation);
		return true;
	}
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
	
	CmpEq,
	CmpNe,
	CmpGt,
	CmpLt,
	CmpGe,
	CmpLe,
}