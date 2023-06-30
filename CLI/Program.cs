using CommandLine;

namespace Squyrm.CLI;

public static class Program
{
	public static int Main(string[] args)
	{
		return CommandLine.Parser.Default.ParseArguments<ReplOptions, BuildOptions, ParseOptions>(args).MapResult(
			(ReplOptions options) => Repl.Execute(options),
			(BuildOptions options) => Build.Execute(options),
			(ParseOptions options) => Parse.Execute(options),
			_ => 1
		);
	}
}