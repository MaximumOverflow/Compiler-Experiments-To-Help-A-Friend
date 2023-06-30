namespace Squyrm.Compiler;

internal sealed class FileCompilationContext
{
	public readonly CompilationContext GlobalContext;

	public readonly Namespace Namespace;
	public readonly Dictionary<ReadOnlyMemory<char>, Type> ImportedTypes;
	public readonly Dictionary<ReadOnlyMemory<char>, Function> ImportedFunctions;

	public FileCompilationContext(CompilationContext context, Namespace @namespace)
	{
		Namespace = @namespace;
		GlobalContext = context;
		ImportedTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance);
		ImportedFunctions = new Dictionary<ReadOnlyMemory<char>, Function>(MemoryStringComparer.Instance);
	}

	public void DefineTypes(IReadOnlyList<IRootDeclarationNode> declarations)
	{
		foreach (var decl in declarations)
		{
			if(decl is not StructNode @class) continue;
			var type = new StructType(
				GlobalContext, @class.Name, 
				GlobalContext.LlvmContext.CreateNamedStruct(@class.Name.Span)
			)
			{
				Members = new Dictionary<ReadOnlyMemory<char>, TypeMember>(@class.Members.Count, MemoryStringComparer.Instance),
			};
			Namespace.Types.Add(@class.Name, type);
		}

		foreach (var decl in declarations)
		{
			if(decl is not StructNode @class) continue;
			var type = (StructType) Namespace.Types[@class.Name];
			var memberTypes = new LLVMTypeRef[@class.Members.Count];
			var membersDict = (Dictionary<ReadOnlyMemory<char>, TypeMember>) type.Members;
			for (var i = 0; i < @class.Members.Count; i++)
			{
				var memberDef = @class.Members[i];
				var memberType = FindType(memberDef.Type);
				
				memberTypes[i] = memberType;
				membersDict.Add(memberDef.Name, new TypeMember
				{
					Idx = (uint) i,
					Name = memberDef.Name,
					Type = memberType,
				});
			}
			
			type.LlvmType.StructSetBody(memberTypes, false);
		}
	}

	public void DefineFunctions(IReadOnlyList<IRootDeclarationNode> declarations)
	{
		foreach (var decl in declarations)
		{
			switch (decl)
			{
				case FunctionNode function:
				{
					var returnType = FindType(function.ReturnType);
					var parameterNames = function.Parameters.Select(p => p.Name).ToArray();
					var parameterTypes = function.Parameters.Select(p => FindType(p.Type)).ToArray();
					var functionType = new FunctionType(returnType, parameterTypes, function.Variadic);
					
					Namespace.Functions.Add(
						function.Name, 
						new Function(GlobalContext, function.Name, function.Public, functionType, parameterNames)
					);

					break;
				}
			}
		}

		foreach (var decl in declarations)
		{
			if (decl is not FunctionNode {Name: var name, Block: {} block}) continue;
			var func = Namespace.Functions[name];
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