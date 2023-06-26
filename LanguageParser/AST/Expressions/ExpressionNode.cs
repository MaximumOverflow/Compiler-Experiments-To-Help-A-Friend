using LanguageParser.Parser;

namespace LanguageParser.AST;

internal abstract class ExpressionNode : AstNode, IStatementNode, IParameterizedParseableNode<ExpressionNode, ExpressionNode.ParsingParams>
{
	internal record struct ParsingParams(bool Greedy, int RecursionChances)
	{
		public static readonly ParsingParams Default = new(false, 32);
	}

	public static bool TryParse(ref TokenStream stream, ParsingParams args, out ExpressionNode result)
	{
		result = default!;
		var tokens = stream;
		
		if (!tokens.Valid || args.RecursionChances == 0) 
			return false;

		result = true switch
		{
			true when ConstantNode.TryParse(ref tokens, args.Greedy, out var res) => res,
			true when VariableNode.TryParse(ref tokens, args.Greedy, out var res) => res,
			true when NewNode.TryParse(ref tokens, args.RecursionChances, out var res) => res,
			true when BlockNode.TryParse(ref tokens, out var res) => res,
			true when BinaryOperationNode.TryParse(ref tokens, args.RecursionChances, out var res) => res,
			_ => throw new UnexpectedTokenException(tokens.Current!.Value),
		};

		stream = tokens;
		return true;
	}
}