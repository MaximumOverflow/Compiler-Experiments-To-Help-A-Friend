namespace Squyrm.Parser.AST;

public sealed class RootNode : IParseableNode<RootNode>
{
	public ReadOnlyMemory<char> Namespace { get; }
	public IReadOnlyList<ImportNode> Imports { get; }
	public IReadOnlyList<IRootDeclarationNode> Declarations { get; }

	internal RootNode(ReadOnlyMemory<char> ns, IReadOnlyList<ImportNode> imports, IReadOnlyList<IRootDeclarationNode> declarations)
	{
		Namespace = ns;
		Imports = imports;
		Declarations = declarations;
	}

	public static bool TryParse(ref TokenStream stream, out RootNode result)
	{
		result = default!;
		var tokens = stream;

		if (!tokens.Valid)
			return false;
		
		if(!ImportNode.TryParseNamespace(ref tokens, out var @namespace))
			return UnexpectedTokenException.Throw<bool>(tokens.Current);

		var imports = new List<ImportNode>();
		while (ImportNode.TryParse(ref tokens, out var import))
			imports.Add(import);

		var declarations = new List<IRootDeclarationNode>();
		while (tokens.Valid)
		{
			var decl = true switch
			{
				true when StructNode.TryParse(ref tokens, out var res) => res,
				true when FunctionNode.TryParse(ref tokens, out var res) => res,
				_ => UnexpectedTokenException.Throw<IRootDeclarationNode>(tokens.Current),
			};
			declarations.Add(decl);
		}

		stream = tokens;
		result = new RootNode(@namespace, imports, declarations);
		return true;
	}
}