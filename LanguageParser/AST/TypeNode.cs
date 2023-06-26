using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class TypeNode : AstNode, IParseableNode<TypeNode>
{
	public required ReadOnlyMemory<char> Name { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out TypeNode result)
	{
		result = default!;
		var tokens = stream;
		
		switch (tokens.MoveNext())
		{
			case {Type: TokenType.Name or TokenType.Str or TokenType.Num or TokenType.Void} token: 
				result = new TypeNode { Name = token.Text };
				break;

			case null: throw new EndOfStreamException();
			default: return false;
		}

		stream = tokens;
		return true;
	}
}