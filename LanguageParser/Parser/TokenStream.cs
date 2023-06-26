using LanguageParser.Tokenizer;
using LanguageParser.AST;

namespace LanguageParser.Parser;

public ref struct TokenStream
{
	public int Position;
	public int Length => _tokens.Length;
	private readonly Span<Token> _tokens;

	public TokenStream(Span<Token> tokens)
	{
		_tokens = tokens;
	}

	public bool Valid => Position < Length;
	
	public Token? Current => Position < Length
		? _tokens[Position]
		: null;
	
	public Token? Next => Position < Length - 1
		? _tokens[Position + 1]
		: null;

	public Token? MoveNext()
	{
		switch (Current)
		{
			case {} token:
				Position++;
				return token;
			
			case null:
				return null;
		}
	}
	
	public bool MoveNext(out Token nextToken)
	{
		Position++;
		if (Current is {} token)
		{
			nextToken = token;
			return true;
		}
		else
		{
			nextToken = default;
			return false;
		}
	}

	public bool ExpectToken(TokenType type)
	{
		return ExpectToken(type, out Token _);
	}

	public bool ExpectToken(TokenType type, out Token token)
	{
		if (MoveNext() is not {} t)
		{
			token = default;
			return false;
		}

		token = t;
		return t.Type == type ? true : throw new UnexpectedTokenException(t, type);
	}

	public bool ExpectToken(TokenType type, out ReadOnlyMemory<char> text)
	{
		if (!ExpectToken(type, out Token token))
		{
			text = default;
			return false;
		}

		text = token.Text;
		return true;
	}
}