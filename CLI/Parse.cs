using System.Runtime.InteropServices;
using Squyrm.Parser.AST;
using Squyrm.Utilities;
using Squyrm.Parser;
using CommandLine;
using Pastel;

namespace Squyrm.CLI;

[Verb("parse", HelpText = "Parse a Squyrm file and print its AST.")]
public sealed class ParseOptions
{
	[Value(0, MetaName = "path", HelpText = "The path to the file to parse.")]
	public required string Path { get; init; }
	
	[Option("echo", Default = false)]
	public bool Echo { get; init; }
	
	[Option("show-tokens", Default = true)]
	public bool ShowTokens { get; init; }
}

public static class Parse
{
	public static int Execute(ParseOptions options)
	{
		try
		{
			var code = File.ReadAllText(options.Path);
			var tokens = Tokenizer.Tokenize(code);
			var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));

			if (options.ShowTokens)
			{
				Console.WriteLine(string.Join('\n', tokens).Pastel(ConsoleColor.Blue));
				Console.WriteLine();
			}
	
			if (!RootNode.TryParse(ref stream, out var root)) 
				throw new Exception("Failed to parse root node.");

			Console.WriteLine(((IAstNode) root).GetDebugString("    ").Pastel(ConsoleColor.Blue));
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message.Pastel(ConsoleColor.Red));
			return 1;
		}

		return 0;
	}
}