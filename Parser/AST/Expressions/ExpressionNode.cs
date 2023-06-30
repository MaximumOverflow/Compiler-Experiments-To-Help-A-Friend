namespace Squyrm.Parser.AST;

public interface IExpressionNode : IStatementNode
{
	public static bool TryParse(ref TokenStream stream, bool greedy, out IExpressionNode result)
	{
		result = default!;
		var tokens = stream;

		switch (greedy)
		{
			case false when BinaryOperationNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when UnaryOperationNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when ConstantNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when VariableNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when NewNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when BlockNode.TryParse(ref tokens, out var node): result = node; break;
			default: return false;
		}

		stream = tokens;
		return true;
	}
}