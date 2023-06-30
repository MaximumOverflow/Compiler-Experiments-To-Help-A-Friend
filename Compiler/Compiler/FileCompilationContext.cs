using System.Collections.Immutable;

namespace Squyrm.Compiler;

internal sealed class FileCompilationContext
{
	public readonly CompilationContext GlobalContext;

	public readonly Namespace Namespace;
	public readonly Dictionary<ReadOnlyMemory<char>, Type> ImportedTypes;
	public readonly Dictionary<ReadOnlyMemory<char>, Function> ImportedFunctions;

	public readonly string? FilePath;
	private readonly List<IRootDeclarationNode> _declarationNodes;

	public FileCompilationContext(CompilationContext context, Namespace @namespace, string? filePath = null)
	{
		FilePath = filePath;
		Namespace = @namespace;
		GlobalContext = context;
		ImportedTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance);
		ImportedFunctions = new Dictionary<ReadOnlyMemory<char>, Function>(MemoryStringComparer.Instance);
		_declarationNodes = new List<IRootDeclarationNode>();
	}

	public void DeclareSymbols(IReadOnlyList<IRootDeclarationNode> declarations)
	{
		_declarationNodes.AddRange(declarations);
		foreach (var d in declarations)
		{
			switch (d)
			{
				case StructNode decl:
				{
					var type = StructType.Create(GlobalContext, decl.Name);
					Namespace.Types.Add(decl.Name, type);
					break;
				}
				
				case FunctionNode decl:
				{
					var returnType = FindType(decl.ReturnType);
					var parameterNames = decl.Parameters.Select(p => p.Name).ToArray();
					var parameterTypes = decl.Parameters.Select(p => FindType(p.Type)).ToArray();
					var functionType = new FunctionType(returnType, parameterTypes, decl.Variadic);

					var function = new Function(GlobalContext, decl.Name, decl.Public, functionType, parameterNames);
					Namespace.Functions.Add(decl.Name, function);
					break;
				}

				default: 
					throw new NotImplementedException();
			}
		}
	}

	public void CompileSymbols()
	{
		foreach (var d in _declarationNodes)
		{
			if(d is not StructNode decl) continue;
			var type = (StructType) Namespace.Types[decl.Name];
			var membersList = new (ReadOnlyMemory<char>, Type)[decl.Members.Count];
					
			for (var i = 0; i < decl.Members.Count; i++)
			{
				var memberDef = decl.Members[i];
				var memberType = FindType(memberDef.Type);
				membersList[i] = (memberDef.Name, memberType);
			}
			
			type.SetBody(membersList);
		}

		foreach (var d in _declarationNodes)
		{
			if(d is not FunctionNode {Block: {} block } decl) continue;
			var func = Namespace.Functions[decl.Name];
			Function.SetBody(this, func, block);
		}
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