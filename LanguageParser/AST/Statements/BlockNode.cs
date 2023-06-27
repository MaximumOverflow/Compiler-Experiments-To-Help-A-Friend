using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class BlockNode : ExpressionNode, IStatementNode, IParseableNode<BlockNode>
{
	public IReadOnlyList<IStatementNode> StatementNodes { get; }

	public BlockNode(IReadOnlyList<IStatementNode> statements)
	{
		StatementNodes = statements;
	}
	
	public static bool TryParse(ref TokenStream stream, out BlockNode result)
	{
		result = default!;
		var tokens = stream;
		if (tokens.MoveNext() is not {Type: TokenType.OpeningBracket})
			return false;
		
		var statements = new List<IStatementNode>();
		while (tokens.Current is { Type: not TokenType.ClosingBracket } current)
		{
			IStatementNode statement = true switch
			{
				true when IfNode.TryParse(ref tokens, out var s) => s,
				true when ForNode.TryParse(ref tokens, out var s) => s,
				true when WhileNode.TryParse(ref tokens, out var s) => s,
				true when AssignmentNode.TryParse(ref tokens, out var s) => s,
				true when VarDeclNode.TryParse(ref tokens, out var s) => s,
				true when ReturnNode.TryParse(ref tokens, out var s) => s,
				true when ExpressionNode.TryParse(ref tokens, false, out var s) => s,
				_ => throw new UnexpectedTokenException(current),
			};

			statements.Add(statement);
		}

		tokens.MoveNext();
		
		stream = tokens;
		result = new BlockNode(statements);
		return true;
	}
}