namespace Squyrm.Compiler.Compiler.Passes;

internal static class SymbolCompilationPass
{
	internal static void Execute(
		IReadOnlyDictionary<string, RootNode> fileRoots,
		IReadOnlyDictionary<string, TranslationUnit> translationUnits
	)
	{
		foreach (var (path, context) in translationUnits)
		{
			var root = fileRoots[path];
			
			foreach (var decl in root.Declarations.OfType<StructNode>())
			{
				var type = (StructType) context.Namespace.Types[decl.Name];
				var membersList = new (ReadOnlyMemory<char>, Type)[decl.Members.Count];
					
				for (var i = 0; i < decl.Members.Count; i++)
				{
					var memberDef = decl.Members[i];
					var memberType = context.FindType(memberDef.Type);
					membersList[i] = (memberDef.Name, memberType);
				}
			
				type.SetBody(membersList);
			}

			foreach (var decl in root.Declarations)
			{
				if(decl is not FunctionNode {Block: {} block } fn) continue;
				var func = context.Namespace.Functions[fn.Name];
				Function.SetBody(context, func, block);
			}
		}
	}
}