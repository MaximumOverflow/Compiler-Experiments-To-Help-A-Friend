using LanguageParser.Tokenizer;
using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class FunctionNode : IRootDeclarationNode, IParseableNode<FunctionNode>
{
	public required bool Public { get; init; }
	public required bool Variadic { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required TypeNode ReturnType { get; init; }
	public required IReadOnlyList<ParameterNode> Parameters { get; init; }
	public required BlockNode? Block { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out FunctionNode result)
	{
		result = default!;
		var tokens = stream;

		bool @public, @extern = false;
		switch (tokens.MoveNext())
		{
			case {Type: TokenType.Public}: @public = true; break;
			case {Type: TokenType.Private}: @public = false; break;
			case { Type: TokenType.External }:
			{
				@extern = true;
				@public = tokens.MoveNext() switch
				{
					{ Type: TokenType.Public } => true,
					{ Type: TokenType.Private } => false,
					var token => UnexpectedTokenException.Throw<bool>(token),
				};
				break;
			}
			default: return false;
		}

		if (!TypeNode.TryParse(ref tokens, out var returnType))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);
		
		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;

		if (!ParameterNode.TryParse(ref tokens, out (IReadOnlyList<ParameterNode>, bool) argsTuple))
			return false;

		var (args, variadic) = argsTuple;

		BlockNode? block = null;
		if (@extern) tokens.ExpectToken(TokenType.Semicolon);
		else if (!BlockNode.TryParse(ref tokens, out block))
			return false;

		stream = tokens;
		result = new FunctionNode
		{
			Public = @public,
			Name = name,
			Block = block,
			Parameters = args,
			Variadic = variadic,
			ReturnType = returnType,
		};
		
		return true;
	}
}

internal sealed class ParameterNode : IParseableNode<ParameterNode>, IParseableNode<(IReadOnlyList<ParameterNode>, bool)>
{
	public required ReadOnlyMemory<char> Name { get; init;  }
	public required TypeNode Type { get; init;  }
	
	public static bool TryParse(ref TokenStream stream, out ParameterNode result)
	{
		result = default!;
		var tokens = stream;
		
		if(!TypeNode.TryParse(ref tokens, out var type))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;
		
		stream = tokens;
		result = new ParameterNode { Name = name, Type = type };
		return true;
	}

	public static bool TryParse(ref TokenStream stream, out (IReadOnlyList<ParameterNode>, bool) result)
	{
		result = default!;
		var tokens = stream;

		tokens.ExpectToken(TokenType.OpenRound);

		var variadic = false;
		var args = new List<ParameterNode>();
		while (true)
		{
			if (tokens.Current is { Type: TokenType.CloseRound })
			{
				tokens.MoveNext();
				break;
			}

			if (tokens.Current is { Type: TokenType.VariadicExpansion })
			{
				tokens.MoveNext();
				tokens.ExpectToken(TokenType.CloseRound);
				variadic = true;
				break;
			}
			
			if (!TryParse(ref tokens, out ParameterNode param)) 
				return false;
			
			args.Add(param);
			
			if (tokens.Current is { Type: TokenType.CloseRound })
			{
				tokens.MoveNext();
				break;
			}

			if (!tokens.ExpectToken(TokenType.Comma))
				return false;
		}

		result = (args, variadic);
		stream = tokens;
		return true;
	}
}