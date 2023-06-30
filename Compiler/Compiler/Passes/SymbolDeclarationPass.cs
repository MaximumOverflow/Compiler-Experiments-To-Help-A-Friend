namespace Squyrm.Compiler.Passes;

public static class SymbolDeclarationPass
{
	internal static IReadOnlyDictionary<string, TranslationUnit> Execute
	(
		CompilationContext compilationContext,
		IReadOnlyDictionary<string, RootNode> fileRoots
	)
	{
		var translationUnits = new Dictionary<string, TranslationUnit>();
		foreach (var (path, root) in fileRoots)
		{
			if(!compilationContext.Namespaces.TryGetValue(root.Namespace, out var ns))
				compilationContext.Namespaces.Add(root.Namespace, ns = new Namespace(root.Namespace));
			
			try
			{
				var unit = new TranslationUnit(compilationContext, ns, path);
				Execute(unit, root);
				translationUnits.Add(path, unit);
			}
			catch (Exception e)
			{
				throw new CompilationException($"Failed to compile file '{path}'.", e);
			}
		}
		
		return translationUnits;
	}

	internal static void Execute(TranslationUnit context, RootNode root)
	{
		foreach (var decl in root.Declarations.OfType<StructNode>())
		{
			var type = StructType.Create(context.GlobalContext, decl.Name);
			context.Namespace.Types.Add(decl.Name, type);
		}

		foreach (var decl in root.Declarations.OfType<FunctionNode>())
		{
			var returnType = context.FindType(decl.ReturnType);
			var parameterNames = decl.Parameters.Select(p => p.Name).ToArray();
			var parameterTypes = decl.Parameters.Select(p => context.FindType(p.Type)).ToArray();
			var functionType = new FunctionType(returnType, parameterTypes, decl.Variadic);

			var function = new Function(context.GlobalContext, decl.Name, decl.Public, functionType, parameterNames);
			context.Namespace.Functions.Add(decl.Name, function);
		}
	}
}