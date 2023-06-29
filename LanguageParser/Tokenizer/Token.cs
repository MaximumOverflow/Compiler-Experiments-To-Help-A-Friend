using System.Runtime.InteropServices;

namespace LanguageParser.Tokenizer;

public readonly struct Token
{
	public required TokenType Type { get;  init; }
	public ReadOnlyMemory<char> Text { get; init; }

	public int Begin
	{
		get
		{
			MemoryMarshal.TryGetString(Text, out _, out var start, out _);
			return start;
		}
	}
	
	public int End
	{
		get
		{
			MemoryMarshal.TryGetString(Text, out _, out var start, out var length);
			return start + length;
		}
	}

	public override string ToString() => Text.IsEmpty
		? $"Token<{Type}>"
		: $"Token<{Type}>(\"{Text}\")";
}

public enum TokenType
{
	// ReSharper disable once InconsistentNaming
	EOF = default,
	
	OpenCurly,
	OpenRound,
	OpenSquare,
	CloseRound,
	CloseCurly,
	CloseSquare,

	AssignmentSeparator,
	Set,
	Comma,
	Semicolon,
	Float,
	Int,
	NamespaceTag,
	Name,
	Period,
	Addition,
	Subtraction,
	Multiplication,
	Division,
	Exponential,
	LessThan,
	LessThanOrEqual,
	LargerThan,
	LargerThanOrEqual,
	Equal,
	NotEqual,
	LogicalAnd,
	LogicalOr,
	LogicalNand,
	Class,
	If,
	Var,
	New,
	Else,
	Public,
	Private,
	External,
	Void,
	Return,
	Nix,
	True,
	False,
	String,
	Import,
	Modulo,
	Throw,
	Const,
	While,
	LogicalXor,
	Or,
	And,
	Not,
	Xor,
	Nand,
	Quote,
	Range,
	VariadicExpansion,
	For,
	In,
	Static,
	Undefined,
	TypeId,
}

internal static class TokenExtensions
{
	public static bool IsBinaryOp(this TokenType type) => type switch
	{
		TokenType.Addition or TokenType.Subtraction or 
			TokenType.Multiplication or TokenType.Modulo or 
			TokenType.Division or TokenType.Exponential or TokenType.Range 
			or TokenType.Equal or TokenType.NotEqual or TokenType.LargerThan or TokenType.LargerThanOrEqual
			or TokenType.LessThan or TokenType.LessThanOrEqual
			=> true,
		_ => false,
	};
}