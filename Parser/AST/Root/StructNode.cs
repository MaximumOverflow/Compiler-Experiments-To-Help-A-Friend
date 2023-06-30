namespace Squyrm.Parser.AST;

public sealed class StructNode : IRootDeclarationNode, IParseableNode<StructNode>
{
	public bool Public { get; }
	public ReadOnlyMemory<char> Name { get; }
	public IReadOnlyList<ClassMemberNode> Members { get; }

	internal StructNode(bool @public, ReadOnlyMemory<char> name, IReadOnlyList<ClassMemberNode> members)
	{
		Public = @public;
		Name = name;
		Members = members;
	}

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
		result = new StructNode(@public, name, members);
		return true;
	}
}

public sealed class ClassMemberNode : IParseableNode<ClassMemberNode>
{
	public bool Public { get; }
	public bool Const { get; }
	public ReadOnlyMemory<char> Name { get; }
	public TypeNode Type { get; }

	internal ClassMemberNode(bool @public, bool @const, ReadOnlyMemory<char> name, TypeNode type)
	{
		Public = @public;
		Const = @const;
		Name = name;
		Type = type;
	}

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
		result = new ClassMemberNode(@public, @const, name, type);
		return true;
	}
}