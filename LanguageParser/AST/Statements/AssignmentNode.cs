using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class AssignmentNode : AstNode, IStatementNode, IParseableNode<AssignmentNode>
{
	public required VariableNode Left { get; init; }
	public required ExpressionNode Right { get; init; }

	public static bool TryParse(ref TokenStream stream, out AssignmentNode result)
	{
		result = default!;
		var tokens = stream;
		if (tokens.MoveNext() is not { Type: TokenType.Set }) return false;
		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name)) return false;
		if (!tokens.ExpectToken(TokenType.AssignmentSeparator)) return false;

		if (!ExpressionNode.TryParse(ref tokens, ExpressionNode.ParsingParams.Default, out var value))
			return false;

		if (!tokens.ExpectToken(TokenType.Semicolon))
			return false;
		
		stream = tokens;
		result = new AssignmentNode
		{
			Left = new VariableNode { Name = name },
			Right = value,
		};

		return true;

	}
}