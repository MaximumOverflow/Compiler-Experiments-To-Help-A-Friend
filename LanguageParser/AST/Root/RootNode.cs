using LanguageParser.Parser;

namespace LanguageParser.AST;

internal sealed class RootNode : AstNode, IParseableNode<RootNode>
{
	public required ReadOnlyMemory<char> Namespace { get; init; }
	public required IReadOnlyList<ImportNode> Imports { get; init; }
	public required IReadOnlyList<IRootDeclarationNode> Declarations { get; init; }

	public static bool TryParse(ref TokenStream stream, out RootNode result)
	{
		result = default!;
		var tokens = stream;

		if (!tokens.Valid)
			return false;
		
		if(!ImportNode.TryParseNamespace(ref tokens, out var @namespace))
			throw new UnexpectedTokenException(tokens.Current ?? throw new EndOfStreamException());

		var imports = new List<ImportNode>();
		while (ImportNode.TryParse(ref tokens, out var import))
			imports.Add(import);

		var declarations = new List<IRootDeclarationNode>();
		while (tokens.Valid)
		{
			IRootDeclarationNode decl = true switch
			{
				true when ClassNode.TryParse(ref tokens, out var res) => res,
				true when FunctionNode.TryParse(ref tokens, out var res) => res,
				_ => throw new UnexpectedTokenException(tokens.Current!.Value),
			};
			declarations.Add(decl);
		}

		stream = tokens;
		result = new RootNode
		{
			Namespace = @namespace,
			Imports = imports,
			Declarations = declarations,
		};
		return true;
	}
}