using System.Diagnostics;

namespace Squyrm.Parser.AST;

public partial interface IAstNode {}
public interface IRootDeclarationNode : IAstNode {}

public interface IStatementNode : IAstNode
{
	public bool RequiresSemicolon => true;
}


public interface IParseableNode<TSelf> : IAstNode
{
	public static abstract bool TryParse(ref TokenStream stream, out TSelf result);
}

public interface IParameterizedParseableNode<TSelf, in TParams>
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

	internal UnexpectedTokenException(Token token, TokenType? expected = null)
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

	internal static T Throw<T>(Token? token, TokenType? expected = null)
	{
		Debugger.Break();
		throw new UnexpectedTokenException(token ?? throw new EndOfStreamException(), expected);
	}
}