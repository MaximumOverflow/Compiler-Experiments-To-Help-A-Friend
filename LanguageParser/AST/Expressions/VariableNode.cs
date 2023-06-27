using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class VariableNode : ExpressionNode, IParseableNode<VariableNode>
{
	public required ReadOnlyMemory<char> Name { get; init; }

	public static bool TryParse(ref TokenStream stream, out VariableNode result)
	{
		result = default!;
		var tokens = stream;

		if(tokens.MoveNext() is not {Type: TokenType.Name} token)
			return false;

		stream = tokens;
		result = new VariableNode { Name = token.Text };
		return true;
	}
}