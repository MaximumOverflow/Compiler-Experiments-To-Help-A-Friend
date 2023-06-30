using System.Text.RegularExpressions;

namespace Squyrm.Parser;

public static class Tokenizer
{
	public static List<Token> Tokenize(string text)
	{
		var tokens = new List<Token>(78);
		var stream = new PeekStream(text);

		while (stream.Current is {} ch)
		{
			switch (ch)
			{
				case '\n' or '\r' or '\t' or ' ': 
					break;
				
				// Skip line comments
				case '/' when stream.Next is '/':
				{
					while (stream.MoveNext(out ch))
					{
						if (ch != '\n') continue;
						break;
					}
					
					break;
				}
				
				// Skip block comments
				case '/' when stream.Next is '*':
				{
					while (stream.MoveNext(out ch))
					{
						if (ch != '*') continue;
						if (!stream.MoveNext(out ch)) break;
						if (ch != '/') continue;
						break;
					}
					
					break;
				}

				// Parse strings
				case '"':
				{
					var last = '"';
					stream.MoveNext();
					var stringText = stream.Peek(ref last, (ref char l, char c) =>
					{
						if (c != '"')
						{
							l = c;
							return true;
						}
						
						if (l != '\\') 
							return false;
						
						l = c;
						return true;
					});
					
					tokens.Add(new Token
					{
						Text = stringText,
						Type = TokenType.String,
					});

					stream.Position += stringText.Length;
					break;
				}
				
				// Parse keywords or names
				case '_':
				case >= 'a' and <= 'z':
				case >= 'A' and <= 'Z':
				{
					var keyword = stream.Peek(c => char.IsLetterOrDigit(c) || c == '_');
					tokens.Add(new Token
					{
						Text = keyword,
						Type = keyword.Span switch
						{
							"as" => TokenType.As,
							"whether" => TokenType.If,
							"within" => TokenType.In,
							"whilst" => TokenType.While,
							"every" => TokenType.For,
							"unless" => TokenType.If,
							"nix" => TokenType.Nix,
							"fresh" => TokenType.New,
							"var" => TokenType.Var,
							"foreign" => TokenType.External,
							"unrelenting" => TokenType.Const,
							"decoupled" => TokenType.Static,
							"mystery" => TokenType.Undefined,
							"set" => TokenType.Set,
							"yes" => TokenType.True,
							"yeet" => TokenType.Throw,
							"otherwise" => TokenType.Else,
							"no" => TokenType.False,
							"thing" => TokenType.Class,
							"wield" => TokenType.Import,
							"accessible" => TokenType.Public,
							"relinquish" => TokenType.Return,
							"inaccessible" => TokenType.Private,
							"__type_id__" => TokenType.TypeId,
							_ => TokenType.Name,
						},
					});

					stream.Position += keyword.Length - 1;
					break;
				}

				case '.':
				{
					var count = 1;
					var clone = stream;
					var type = TokenType.Period;
					
					if (stream.Next == '.')
					{
						stream.MoveNext();
						type = TokenType.Range;
						count++;
					}
					
					if (stream.Next == '.')
					{
						stream.MoveNext();
						type = TokenType.VariadicExpansion;
						count++;
					}

					var tokenText = clone.Peek(count);
					tokens.Add(new Token { Text = tokenText, Type = type });
					break;
				}

				// Parse symbols
				case var _ when char.IsSymbol(ch) || char.IsPunctuation(ch):
				{
					var next = stream.Next;
					var type = ch switch
					{
						',' => TokenType.Comma,
						';' => TokenType.Semicolon,
						'"' => TokenType.Quote,
						
						'(' => TokenType.OpenRound,
						'{' => TokenType.OpenCurly,
						'[' => TokenType.OpenSquare,
						')' => TokenType.CloseRound,
						'}' => TokenType.CloseCurly,
						']' => TokenType.CloseSquare,

						'=' when next is '=' => TokenType.Equal,
						'!' when next is '=' => TokenType.NotEqual,
						'<' when next is '=' => TokenType.LessThanOrEqual,
						'>' when next is '=' => TokenType.LargerThanOrEqual,
						'>' => TokenType.LargerThan,
						'<' => TokenType.LessThan,

						'|' when next is '|' => TokenType.LogicalOr,
						'^' when next is '|' => TokenType.LogicalXor,
						'&' when next is '&' => TokenType.LogicalAnd,
						'~' when next is '&' => TokenType.LogicalNand,
						
						'@' => TokenType.NamespaceTag,

						'^' when next is '^' => TokenType.Exponential,
						'+' => TokenType.Addition,
						'/' => TokenType.Division,
						'%' => TokenType.Modulo,
						'-' => TokenType.Subtraction,
						'*' => TokenType.Multiplication,
						'=' => TokenType.AssignmentSeparator,
						
						'|' => TokenType.Or,
						'&' => TokenType.And,
						'!' => TokenType.Not,
						'^' => TokenType.Xor,
						'~' => TokenType.Nand,

						_ => throw new TokenizerException
						{
							Character = ch,
							Line = stream.CurrentLine,
							Column = stream.CurrentColumn,
							Position = stream.Position,
						},
					};

					var tokenText = type switch
					{
						TokenType.Equal or TokenType.NotEqual or TokenType.LessThanOrEqual
							or TokenType.LargerThanOrEqual or
							TokenType.LogicalAnd or TokenType.LogicalNand or TokenType.LogicalOr or TokenType.LogicalXor
							or TokenType.Exponential or TokenType.Range => stream.Peek(2),
						_ => stream.Peek(1),
					};
					
					tokens.Add(new Token { Text = tokenText, Type = type });

					if (tokenText.Length == 2)
						stream.MoveNext();
					
					break;
				}

				case >= '0' and <= '9':
				{
					var number = stream.Peek((current, next) => char.IsDigit(current) || (
						current == '.' && char.IsDigit(next ?? default)
					));

					tokens.Add(new Token
					{
						Text = number,
						Type = number.Span.Contains('.') ? TokenType.Float : TokenType.Int,
					});
					
					stream.Position += number.Length - 1;
					break;
				}
				
				default: throw new TokenizerException
				{
					Character = ch,
					Line = stream.CurrentLine,
					Column = stream.CurrentColumn,
					Position = stream.Position,
				};
			}

			stream.MoveNext();
		}

		return tokens;
	}
}

public sealed class TokenizerException : Exception
{
	public required char Character { get; init; }
	public required int Position { get; init; }
	public required int Column { get; init; }
	public required int Line { get; init; }
	public string? CustomMessage { get; init; }

	public override string Message => ToString();

	public override string ToString()
	{
		var ch = char.IsControl(Character) ? Regex.Escape(Character.ToString()) : Character.ToString();
		var err = $"Unexpected char '{ch}' at position {Position} | {Line}:{Column}.";
		if (CustomMessage is not null) err = $"{err}\n{CustomMessage}";
		return err;
	}
}