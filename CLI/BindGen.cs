using Squyrm.BindGen;
using CommandLine;
using Pastel;

namespace Squyrm.CLI;

[Verb("bindgen", HelpText = "Parse a C header file and print its Squyrm bindings.")]
public sealed class BindGenOptions
{
	[Value(0, MetaName = "path", HelpText = "The path to the C header file to generate bindings for.")]
	public required string Path { get; init; }
	
	[Option('n', "namespace", Default = "CBindings", HelpText = "The namespace for the generated bindings.")]
	public required string Namespace { get; init; }
}

public static class BindGen
{
	public static int Execute(BindGenOptions options)
	{
		var bindings = CBindingsGenerator.GenerateSquyrmBindings(options.Path, options.Namespace);
		Console.WriteLine(bindings.Pastel(ConsoleColor.Blue));
		return 0;
	}
}