using System.Diagnostics;
using System.Runtime.InteropServices;
using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

public partial interface IAstNode {}
internal interface IRootDeclarationNode : IAstNode {}

internal interface IStatementNode : IAstNode
{
	public bool RequiresSemicolon => true;
}


internal interface IParseableNode<TSelf> : IAstNode
{
	public static abstract bool TryParse(ref TokenStream stream, out TSelf result);
}

internal interface IParameterizedParseableNode<TSelf, in TParams>
{
	public static abstract bool TryParse(ref TokenStream stream, TParams @params, out TSelf result);
}

public sealed class UnexpectedTokenException : Exception
{
	public Token Token { get; }
	public TokenType? Expected { get; }
	public int Column { get; }
	public int Line { get; }

	public override string Message
	{
		get
		{
			var err = $"Unexpected token '{Token.Text}' at position {Token.Begin} | {Line}:{Column}.";
			if (Expected is not null) err = $"{err}\nExpected token of type {Expected}, got {Token.Type}.";
			return err;
		}
	}

	public UnexpectedTokenException(Token token, TokenType? expected = null)
	{
		Token = token;
		Expected = expected;
		if (MemoryMarshal.TryGetString(token.Text, out var text, out var start, out _))
		{
			Line = 1;
			Column = 1;
			for (var i = 0; i < start; i++)
			{
				var ch = text[i];
				if (ch == '\n')
				{
					Line++;
					Column = 1;
				}
				else Column++;
			}
		}
	}

	public static T Throw<T>(Token? token, TokenType? expected = null)
	{
		Debugger.Break();
		throw new UnexpectedTokenException(token ?? throw new EndOfStreamException(), expected);
	}
}