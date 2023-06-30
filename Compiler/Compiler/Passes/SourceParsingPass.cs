namespace Squyrm.Compiler;

internal static class SourceParsingPass
{
	internal static IReadOnlyDictionary<string, RootNode> Execute(IEnumerable<string> filePaths)
	{
		var roots = new Dictionary<string, RootNode>();
		
		foreach (var path in filePaths)
		{
			if(roots.ContainsKey(path))
				continue;
			
			var code = File.ReadAllText(path);
			var tokens = Tokenizer.Tokenize(code);
			var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));
			
			try
			{
				if(RootNode.TryParse(ref stream, out var root))
					roots.Add(path, root);
			}
			catch (Exception e)
			{
				throw new CompilationException($"Failed to parse file '{path}'.", e);
			}
		}

		return roots;
	}
}