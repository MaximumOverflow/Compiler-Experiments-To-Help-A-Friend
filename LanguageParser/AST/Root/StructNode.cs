using LanguageParser.Parser;
using LanguageParser.Tokenizer;

namespace LanguageParser.AST;

internal sealed class StructNode : IRootDeclarationNode, IParseableNode<StructNode>
{
	public required bool Public { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required IReadOnlyList<ClassMemberNode> Members { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out StructNode result)
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

		if (tokens.MoveNext() is not { Type: TokenType.Class })
			return false;

		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;

		if (!tokens.ExpectToken(TokenType.OpenCurly))
			return false;

		var members = new List<ClassMemberNode>();
		while (tokens.Current is not {Type: TokenType.CloseCurly})
		{
			if(ClassMemberNode.TryParse(ref tokens, out var member))
				members.Add(member);
		}

		tokens.ExpectToken(TokenType.CloseCurly);
		
		stream = tokens;
		result = new StructNode
		{
			Name = name,
			Public = @public,
			Members = members,
		};
		return true;
	}
}

internal sealed class ClassMemberNode : IParseableNode<ClassMemberNode>
{
	public required bool Public { get; init; }
	public required bool Const { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required TypeNode Type { get; init; }
	
	public static bool TryParse(ref TokenStream stream, out ClassMemberNode result)
	{
		result = default!;
		var tokens = stream;
		
		var @public = tokens.MoveNext() switch
		{
			{ Type: TokenType.Public } => true,
			{ Type: TokenType.Private } => false,
			null => throw new EndOfStreamException(),
			{} token => UnexpectedTokenException.Throw<bool>(token),
		};

		var @const = false;
		if (tokens.Current is { Type: TokenType.Const })
		{
			tokens.MoveNext();
			@const = true;
		}

		if (!TypeNode.TryParse(ref tokens, out var type))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> name))
			return false;
			
		if (!tokens.ExpectToken(TokenType.Semicolon))
			return false;

		stream = tokens;
		result = new ClassMemberNode
		{
			Const = @const,
			Name = name,
			Public = @public,
			Type = type,
		};
		
		return true;
	}
}