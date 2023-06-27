using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class FunctionNode : AstNode, IRootDeclarationNode, IParseableNode<FunctionNode>
{
	public required bool Public { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required TypeNode ReturnType { get; init; }
	public required IReadOnlyList<ParameterNode> Parameters { get; init; }
	public required BlockNode Block { get; init; }

	public static bool TryParse(ref TokenStream stream, out FunctionNode result)
	{
		result = default!;
		var tokens = stream;
		
		bool @public;
		switch (tokens.MoveNext())
		{
			case {Type: TokenType.Public}: @public = true; break;
			case {Type: TokenType.Private}: @public = false; break;
			default: return false;
		}

		if (!TypeNode.TryParse(ref tokens, out var returnType))
			throw new UnexpectedTokenException(tokens.Current ?? throw new EndOfStreamException());
		
		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;

		if (!tokens.ExpectToken(TokenType.OpeningParentheses))
			return false;
		
		var args = new List<ParameterNode>();
		while (true)
		{
			if (tokens.Current is { Type: TokenType.ClosingParentheses })
			{
				tokens.MoveNext();
				break;
			}
			
			if (!ParameterNode.TryParse(ref tokens, out var param)) 
				return false;
			
			args.Add(param);
			
			if (tokens.Current is { Type: TokenType.ClosingParentheses })
			{
				tokens.MoveNext();
				break;
			}

			if (!tokens.ExpectToken(TokenType.Comma))
				return false;
		}

		if (!BlockNode.TryParse(ref tokens, out var block))
			return false;

		stream = tokens;
		result = new FunctionNode
		{
			Public = @public,
			Name = name,
			Block = block,
			Parameters = args,
			ReturnType = returnType,
		};
		
		return true;
	}
}

internal sealed class ParameterNode : AstNode, IParseableNode<ParameterNode>
{
	public required ReadOnlyMemory<char> Name { get; init;  }
	public required TypeNode Type { get; init;  }
	
	public static bool TryParse(ref TokenStream stream, out ParameterNode result)
	{
		result = default!;
		var tokens = stream;
		
		if(!TypeNode.TryParse(ref tokens, out var type))
			throw new UnexpectedTokenException(tokens.Current ?? throw new EndOfStreamException());

		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;
		
		stream = tokens;
		result = new ParameterNode { Name = name, Type = type };
		return true;
	}
}