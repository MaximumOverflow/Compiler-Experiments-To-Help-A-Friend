namespace Squyrm.Compiler;

internal sealed partial class TranslationUnit
{
	public readonly CompilationContext GlobalContext;

	public readonly Namespace Namespace;
	public readonly Dictionary<ReadOnlyMemory<char>, Type> ImportedTypes;
	public readonly Dictionary<ReadOnlyMemory<char>, Function> ImportedFunctions;

	public readonly string? FilePath;
	private readonly List<IRootDeclarationNode> _declarationNodes;

	public TranslationUnit(CompilationContext context, Namespace @namespace, string? filePath = null)
	{
		FilePath = filePath;
		Namespace = @namespace;
		GlobalContext = context;
		ImportedTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance);
		ImportedFunctions = new Dictionary<ReadOnlyMemory<char>, Function>(MemoryStringComparer.Instance);
		_declarationNodes = new List<IRootDeclarationNode>();
	}
	
	public bool TryFindType(ReadOnlyMemory<char> name, out Type type)
	{
		if (Namespace.Types.TryGetValue(name, out type!))
			return true;
		
		if (ImportedTypes.TryGetValue(name, out type!))
			return true;

		if (GlobalContext.DefaultTypes.TryGetValue(name, out type!))
			return true;

		return false;
	}

	public bool TryFindType(TypeNode node, out Type type)
	{
		switch (node)
		{
			case TypeNameNode named:
				return TryFindType(named.Name, out type);

			case PointerTypeNode ptr:
			{
				if (!TryFindType(ptr.Base, out type))
					return false;

				type = type.MakePointer(ptr.Constant);
				return true;
			}
			
			default: 
				throw new NotImplementedException();
		}
	}

	public Type FindType(string typeName)
		=> FindType(typeName.AsMemory());

	public Type FindType(ReadOnlyMemory<char> name)
	{
		if (TryFindType(name, out var type)) return type;
		throw new Exception($"Type '{name}' not found.");
	}
	
	public Type FindType(TypeNode node)
	{
		if (TryFindType(node, out var type)) return type;
		throw new Exception($"Type '{((IAstNode) node).GetDebugString()}' not found.");
	}

	public bool TryFindFunction(ReadOnlyMemory<char> name, out Function function)
	{
		if (Namespace.Functions.TryGetValue(name, out function!))
			return true;
		
		if (ImportedFunctions.TryGetValue(name, out function!))
			return true;

		return false;
	}
}