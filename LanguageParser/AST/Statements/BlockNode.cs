namespace Squyrm.Parser.AST;

public sealed class BlockNode : IExpressionNode, IStatementNode, IParseableNode<BlockNode>
{
	public bool RequiresSemicolon => false;
	public IReadOnlyList<IStatementNode> StatementNodes { get; }

	internal BlockNode(IReadOnlyList<IStatementNode> statements)
	{
		StatementNodes = statements;
	}
	
	public static bool TryParse(ref TokenStream stream, out BlockNode result)
	{
		result = default!;
		var tokens = stream;
		if (tokens.MoveNext() is not {Type: TokenType.OpenCurly})
			return false;
		
		var statements = new List<IStatementNode>();
		while (tokens.Current is { Type: not TokenType.CloseCurly } current)
		{
			IStatementNode statement = true switch
			{
				_ when IfNode.TryParse(ref tokens, out var s) => s,
				_ when ForNode.TryParse(ref tokens, out var s) => s,
				_ when WhileNode.TryParse(ref tokens, out var s) => s,
				_ when VarDeclNode.TryParse(ref tokens, out var s) => s,
				_ when ReturnNode.TryParse(ref tokens, out var s) => s,
				_ when IExpressionNode.TryParse(ref tokens, false, out var s) => s,
				_ => throw new UnexpectedTokenException(current),
			};

			switch (tokens.Current)
			{
				case { Type: TokenType.CloseCurly } when statement is IExpressionNode:
				{
					tokens.MoveNext();
					statements.Add(statement);

					stream = tokens;
					result = new BlockNode(statements);
					return true;
				}

				case var _ when !statement.RequiresSemicolon:
				{
					statements.Add(statement);
					break;
				}
				
				case { Type: TokenType.Semicolon } when statement.RequiresSemicolon:
				{
					tokens.MoveNext();
					statements.Add(statement);
					break;
				}

				default: 
					return UnexpectedTokenException.Throw<bool>(tokens.Current);
			}
		}

		tokens.ExpectToken(TokenType.CloseCurly);
		
		stream = tokens;
		result = new BlockNode(statements);
		return true;
	}
}