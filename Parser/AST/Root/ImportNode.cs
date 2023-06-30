namespace Squyrm.Parser.AST;

public sealed class ImportNode : IParseableNode<ImportNode>
{
	public ReadOnlyMemory<char> Namespace { get; }

	internal ImportNode(ReadOnlyMemory<char> ns)
	{
		Namespace = ns;
	}

	public static bool TryParse(ref TokenStream stream, out ImportNode result)
	{
		result = default!;
		var tokens = stream;

		if (tokens.MoveNext() is not { Type: TokenType.Import }) 
			return false;

		if (!TryParseNamespace(ref tokens, out var @namespace))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		stream = tokens;
		result = new ImportNode(@namespace);
		return true;
	}

	public static bool TryParseNamespace(ref TokenStream stream, out ReadOnlyMemory<char> result)
	{
		result = default!;
		var tokens = stream;
		
		if (tokens.MoveNext() is not {Type: TokenType.NamespaceTag})
			return false;
		
		if (!tokens.ExpectToken(TokenType.Name, out ReadOnlyMemory<char> startName))
			return false;

		ReadOnlyMemory<char> endName = default;
		while (tokens.Valid)
		{
			var exit = false;
			switch (tokens.MoveNext())
			{
				case {Type: TokenType.Period}: 
					tokens.ExpectToken(TokenType.Name, out endName); 
					break;
				
				case {Type: TokenType.Semicolon}:
					exit = true;
					break;
				
				case {} token:
					return UnexpectedTokenException.Throw<bool>(token, TokenType.Name);
			}

			if (exit) 
				break;
		}

		if (!MemoryMarshal.TryGetString(startName, out var text, out var start, out _))
			throw new InvalidOperationException();

		var end = start + startName.Length;
		if (MemoryMarshal.TryGetString(endName, out _, out var endBeg, out var endLen))
			end = endBeg + endLen;

		stream = tokens;
		result = text.AsMemory(start..end);
		return true;
	}
}