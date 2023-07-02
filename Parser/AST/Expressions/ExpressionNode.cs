namespace Squyrm.Parser.AST;

public interface IExpressionNode : IStatementNode
{
	public static bool TryParse(ref TokenStream stream, bool matchOne, out IExpressionNode result)
	{
		result = default!;
		var tokens = stream;

		switch (matchOne)
		{
			case false when BinaryExpressionNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when UnaryOperationNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when ConstantNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when VariableNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when RoundBracketedValueList.TryParse(ref tokens, out var node): result = node; break;
			case var _ when SquareBracketedValueList.TryParse(ref tokens, out var node): result = node; break;
			case var _ when NewNode.TryParse(ref tokens, out var node): result = node; break;
			case var _ when BlockNode.TryParse(ref tokens, out var node): result = node; break;
			default: return false;
		}

		stream = tokens;
		return true;
	}
}