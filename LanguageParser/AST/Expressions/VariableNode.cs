using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class VariableNode : ExpressionNode, IParameterizedParseableNode<VariableNode, bool>
{
	public required ReadOnlyMemory<char> Name { get; init; }

	public static bool TryParse(ref TokenStream stream, bool greedy, out VariableNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (!greedy && tokens.Next is {} next && next.Type.IsBinaryOp())
			return false;
		
		if(tokens.MoveNext() is not {Type: TokenType.Name} token)
			return false;

		stream = tokens;
		result = new VariableNode { Name = token.Text };
		return true;
	}
}