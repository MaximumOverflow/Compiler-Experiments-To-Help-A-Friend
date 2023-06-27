using LanguageParser.Parser;

namespace LanguageParser.AST;

internal abstract class ExpressionNode : AstNode, IStatementNode, IParameterizedParseableNode<ExpressionNode, bool>
{
	public static bool TryParse(ref TokenStream stream, bool greedy, out ExpressionNode result)
	{
		result = default!;
		var tokens = stream;
		
		result = greedy switch
		{
			false when BinaryOperationNode.TryParse(ref tokens, out var node) => node,
			_ when GroupNode.TryParse(ref tokens, out var node) => node,
			_ when ConstantNode.TryParse(ref tokens, out var node) => node,
			_ when VariableNode.TryParse(ref tokens, out var node) => node,
			_ when NewNode.TryParse(ref tokens, out var node) => node,
			_ when BlockNode.TryParse(ref tokens, out var node) => node,
			_ => throw new UnexpectedTokenException(tokens.Current!.Value),
		};

		stream = tokens;
		return true;
	}
}