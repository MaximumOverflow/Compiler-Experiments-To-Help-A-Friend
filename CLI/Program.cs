using LLVMSharp.Interop;
using CommandLine;

namespace Squyrm.CLI;

public static class Program
{
	public static int Main(string[] args)
	{
		LLVM.LinkInMCJIT();
		LLVM.InitializeAllTargetInfos();
		LLVM.InitializeAllTargets();
		LLVM.InitializeAllTargetMCs();
		LLVM.InitializeAllAsmParsers();
		LLVM.InitializeAllAsmPrinters();
		
		return CommandLine.Parser.Default.ParseArguments<ReplOptions, BuildOptions, ParseOptions, BindGenOptions>(args).MapResult(
			(ReplOptions options) => Repl.Execute(options),
			(BuildOptions options) => Build.Execute(options),
			(ParseOptions options) => Parse.Execute(options),
			(BindGenOptions options) => BindGen.Execute(options),
			_ => 1
		);
	}
}