﻿using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class BinaryOperationNode : IExpressionNode, IParseableNode<BinaryOperationNode>
{
	public BinaryOperationType Operation { get; }
	public IExpressionNode Left { get; }
	public IExpressionNode Right { get; }

	public BinaryOperationNode(IExpressionNode left, IExpressionNode right, BinaryOperationType operation)
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

		if (!IExpressionNode.TryParse(ref tokens, true, out var left))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		BinaryOperationType? op = tokens.MoveNext() switch
		{
			{ Type: TokenType.Period } => BinaryOperationType.Access,
			{ Type: TokenType.Addition } => BinaryOperationType.Addition,
			{ Type: TokenType.Division } => BinaryOperationType.Division,
			{ Type: TokenType.Exponential } => BinaryOperationType.Exponential,
			{ Type: TokenType.Subtraction } => BinaryOperationType.Subtraction,
			{ Type: TokenType.Multiplication } => BinaryOperationType.Multiplication,
			{ Type: TokenType.Modulo } => BinaryOperationType.Modulo,
			{ Type: TokenType.Range } => BinaryOperationType.Range,
			{ Type: TokenType.Equal } => BinaryOperationType.CmpEq,
			{ Type: TokenType.NotEqual } => BinaryOperationType.CmpNe,
			{ Type: TokenType.LargerThan } => BinaryOperationType.CmpGt,
			{ Type: TokenType.LessThan } => BinaryOperationType.CmpLt,
			{ Type: TokenType.LargerThanOrEqual } => BinaryOperationType.CmpGe,
			{ Type: TokenType.LessThanOrEqual } => BinaryOperationType.CmpLe,
			{ Type: TokenType.OpenRound } => BinaryOperationType.Call,
			{ Type: TokenType.OpenSquare } => BinaryOperationType.Indexing,
			{ Type: TokenType.AssignmentSeparator } => BinaryOperationType.Assign,
			_ => null,
		};

		if (op is not {} operation)
			return false;

		IExpressionNode right;
		switch (operation)
		{
			case BinaryOperationType.Call:
			{
				tokens.Position--;
				if (!TupleNode.TryParse(ref tokens, out var tuple))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
				right = tuple;
				break;
			}

			case BinaryOperationType.Indexing:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out var index))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
				tokens.ExpectToken(TokenType.CloseSquare);
				right = index;
				break;
			}

			default:
			{
				if (!IExpressionNode.TryParse(ref tokens, false, out right))
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
				
				if (right is BinaryOperationNode bin && GetPriority(operation) >= GetPriority(bin.Operation))
				{
					right = bin.Right;
					left = new BinaryOperationNode(left, bin.Left, operation);
					operation = bin.Operation;
				}
				
				break;
			}
		}

		stream = tokens;
		result = new BinaryOperationNode(left, right, operation);
		return true;
	}

	private static int GetPriority(BinaryOperationType operation) => operation switch
	{
		BinaryOperationType.Access => 3,
		
		BinaryOperationType.Modulo => 2,
		BinaryOperationType.Division => 2,
		BinaryOperationType.Multiplication => 2,
		
		BinaryOperationType.Addition => 1,
		BinaryOperationType.Subtraction => 1,
		
		_ => 0,
	};
}

public enum BinaryOperationType
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
	Assign
}