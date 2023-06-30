using LLVMSharp.Interop;
using System.Text;
using CommandLine;
using Pastel;
using Squyrm.Compiler;

namespace Squyrm.CLI;

[Verb("repl", true, HelpText = "Start the Squyrm REPL.")]
public sealed class ReplOptions
{
	[Option('O', HelpText = "The level of optimization to apply.")]
	public uint OptimizationLevel { get; init; }
}

public static class Repl
{
	private delegate int MainDelegate();

	public static int Execute(ReplOptions options)
	{
		LLVM.LinkInMCJIT();
		LLVM.InitializeX86TargetInfo();
		LLVM.InitializeX86Target();
		LLVM.InitializeX86TargetMC();
		LLVM.InitializeX86AsmParser();
		LLVM.InitializeX86AsmPrinter();

		var valid = true;
		var prevState = string.Empty;
		var script = new StringBuilder("@Repl;\n");
		while (true)
		{
			Console.ForegroundColor = valid ? ConsoleColor.Green : ConsoleColor.Red;
			Console.Write("|>> ");
			var line = Console.ReadLine() ?? string.Empty;
			if (line.StartsWith("$/"))
			{
				var args = line.Split(' ');
				var exit = CommandLine.Parser.Default.ParseArguments<Run, Clear, Print, Exit>(args).MapResult(
					(Run run) =>
					{
						try
						{
							using var context = new CompilationContext(new CompilationSettings
							{
								ModuleName = "Repl",
								EmitReflectionInformation = true,
								OptimizationLevel = options.OptimizationLevel,
							});
							
							context.CompileSourceCode(script.ToString());
							context.FinalizeCompilation();
						
							var main = context.LlvmModule.GetNamedFunction("main");
							if (main == default) return false;
							if (main.ParamsCount != 0) return false;
							var engine = context.LlvmModule.CreateExecutionEngine();
							var mainFn = engine.GetPointerToGlobal<MainDelegate>(main);

							Console.ForegroundColor = ConsoleColor.Blue;
							Console.WriteLine("\nExecuting program...");
							Console.WriteLine($"\nProgram exited with value {mainFn()}.");
						}
						catch (Exception e)
						{
							valid = false;
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine(e.Message);
						}

						return false;
					},
					(Clear clear) =>
					{
						Console.Clear();
						if (clear.ClearScript)
						{
							valid = true;
							script.Clear();
							script.AppendLine("@Repl;");
							prevState = string.Empty;
							Console.ForegroundColor = ConsoleColor.Green;
						}
						return false;
					},
					(Print _) =>
					{
						Console.WriteLine(script.ToString().Pastel(ConsoleColor.Blue));
						return false;
					},
					(Exit _) =>
					{
						return true;
					},
					_ => false
				);

				if (exit)
					break;
			}
			else
			{
				script.AppendLine(line);
				while (true)
				{
					line = Console.ReadLine() ?? string.Empty;
					if(line == ";;") break;
					script.AppendLine(line);
				}
			}
		}

		return 0;
	}
	
	[Verb("$/run")]
	private sealed class Run
	{
		
	}
	
	[Verb("$/clear")]
	private sealed class Clear
	{
		[Value(0, Default = false, HelpText = "Clear the currently buffered script.")]
		public bool ClearScript { get; init; }
	}
	
	[Verb("$/print")]
	private sealed class Print
	{
		
	}
	
	[Verb("$/exit")]
	private sealed class Exit
	{
		
	}
}