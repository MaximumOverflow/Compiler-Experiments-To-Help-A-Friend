namespace Squyrm.Parser.AST;

public sealed class VariableNode : IExpressionNode, IParseableNode<VariableNode>
{
	public ReadOnlyMemory<char> Name { get; }

	internal VariableNode(ReadOnlyMemory<char> name)
	{
		Name = name;
	}

	public static bool TryParse(ref TokenStream stream, out VariableNode result)
	{
		result = default!;
		var tokens = stream;

		if(tokens.MoveNext() is not {Type: TokenType.Name} token)
			return false;

		stream = tokens;
		result = new VariableNode(token.Text);
		return true;
	}
}