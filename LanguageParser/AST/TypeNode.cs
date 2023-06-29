using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal abstract class TypeNode : IParseableNode<TypeNode>
{
	public static bool TryParse(ref TokenStream stream, out TypeNode result)
	{
		result = default!;
		var tokens = stream;
		
		switch (tokens.MoveNext())
		{
			case { Type: TokenType.Multiplication }:
			{
				var @const = false;
				if (tokens.Current is { Type: TokenType.Const })
				{
					tokens.MoveNext();
					@const = true;
				}
				
				if (!TryParse(ref tokens, out var @base))
					return false;
				
				result = new PointerTypeNode { Base = @base, Constant = @const };
				break;
			}
			
			case {Type: TokenType.Name } token: 
				result = new TypeNameNode { Name = token.Text };
				break;

			case null: throw new EndOfStreamException();
			default: return false;
		}

		stream = tokens;
		return true;
	}
}

internal sealed class TypeNameNode : TypeNode
{
	public required ReadOnlyMemory<char> Name { get; init; }
}

internal sealed class PointerTypeNode : TypeNode
{
	public required bool Constant { get; init; }
	public required TypeNode Base { get; init; }
}