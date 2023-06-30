using System.Diagnostics;
using Squyrm.Compiler;
using Squyrm.BindGen;
using System.Text;
using Pastel;

namespace Squyrm;

internal class Program
{
	private bool _isRunning = true;
	private readonly StringBuilder _script = new();

	public static void Main()
	{
		var bindingsStats = new RuntimeStats();
		var bindings = CBindingsGenerator.GenerateSquyrmBindings(
			"C:/Program Files (x86)/Windows Kits/10/Include/10.0.19041.0/ucrt/stdlib.h",
			"@CStdLib.StdIO"
		);
		bindingsStats.Dump("Bindings generation", ConsoleColor.Blue);
            
		try
		{
			var compilationStats = new RuntimeStats();
			var context = new CompilationContext(new CompilationSettings
			{
				ModuleName = "@CStdLib.StdIO", EmitReflectionInformation = true,
			});
	            
			context.CompileSourceCode(bindings);
			context.FinalizeCompilation();
			compilationStats.Dump("Bindings compilation", ConsoleColor.Green);
		}
		catch (Exception e)
		{
			Console.WriteLine(bindings);
			Console.Error.WriteLine(e);
		}
            
		new Program().Run();
	}

	private void Run()
	{
		Console.WriteLine("Welcome to the Squyrm shell.");
		Console.WriteLine("Enter '$/help' to get a list of commands.");
            
		var compilationSettings = new CompilationSettings
		{
			ModuleName = "Test",
			OptimizationLevel = 3,
			EmitReflectionInformation = true,
		};
            
		while (_isRunning)
		{
			Console.Write("|> ");
			var input = Console.ReadLine() ?? string.Empty;
			if (ProcessCommand(input, ref compilationSettings)) 
				continue;
                
			_script.Append(input);
			_script.Append('\n');
		}
	}

	private delegate int  MainDelegate();

	private bool ProcessCommand(string input, ref CompilationSettings settings)
	{
		if (!input.StartsWith("$/"))
		{
			return false;
		}

		if (input == "$/exit")
		{
			_isRunning = false;
			return true;
		}

		if (input.StartsWith("$/opt"))
		{
			try
			{
				settings.OptimizationLevel = bool.Parse(input[6..].Trim()) ? 3u : 0u;
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return false;
		}
            
		if (input.StartsWith("$/emit_reflection_info"))
		{
			try
			{
				settings.EmitReflectionInformation = bool.Parse(input[23..].Trim());
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}

			return false;
		}

		if (input.StartsWith("$/run") || input.StartsWith("$/compile"))
		{
			try
			{
				var start = input.IndexOf("@", StringComparison.Ordinal);
				var source = start != -1 ? File.ReadAllText(input[(start + 1)..]) : _script.ToString();

				CompilationContext context;
				{
					var stats = new RuntimeStats();
					context = new CompilationContext(settings);
					context.CompileSourceCode(source);
					context.FinalizeCompilation();
					stats.Dump("Compilation", ConsoleColor.Green);
				}

				switch (input)
				{
					case var _ when input.StartsWith("$/run"):
					{
						context.LlvmModule.Dump();
						context.LlvmModule.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);

						var main = context.LlvmModule.GetNamedFunction("main");
						if (main == default) return false;
						if (main.ParamsCount != 0) return false;
						var engine = context.LlvmModule.CreateExecutionEngine();
						var mainFn = engine.GetPointerToGlobal<MainDelegate>(main);
			                
						Console.WriteLine("\nExecuting program...");
						Console.WriteLine($"Program exited with value {mainFn()}.");
						break;
					}
			                
					case var _ when input.StartsWith("$/compile"): unsafe
					{	           
						context.LlvmModule.PrintToFile("out.ll");
						var cpu = new string(LLVM.GetHostCPUName());
						var features = new string(LLVM.GetHostCPUFeatures());

						var optLevel = (LLVMCodeGenOptLevel)settings.OptimizationLevel;
						var target = LLVMTargetRef.GetTargetFromTriple(LLVMTargetRef.DefaultTriple);
						var machine = target.CreateTargetMachine(LLVMTargetRef.DefaultTriple, cpu, features,
							optLevel, LLVMRelocMode.LLVMRelocDefault,
							LLVMCodeModel.LLVMCodeModelDefault);

						machine.EmitToFile(context.LlvmModule, "out.asm", LLVMCodeGenFileType.LLVMAssemblyFile);
						Process.Start(new ProcessStartInfo
						{
							FileName = "explorer.exe",
							Arguments = "\"out.ll\"",
							UseShellExecute = true,
						})?.WaitForExit();
						break;
					}
				}
	                
				context.Dispose();
			}
			catch (Exception e)
			{
#if DEBUG
				Console.Error.WriteLine(e.ToString().Pastel(ConsoleColor.Red));
#else
	                Console.Error.WriteLine(e.Message.Pastel(ConsoleColor.Red));
#endif
			}
                
			return true;
		}

		switch (input)
		{
			case "$/clear":
				_script.Clear();
				Console.Clear();
				return true;
	            
			case "$/help":
				Console.WriteLine("- '$/exit' to leave the program.");
				Console.WriteLine("- '$/run' to execute your script. " +
				                  "Adding a path like so '$/run @D:\\user\\scripts\\test.txt' will run the script within that file.");
				Console.WriteLine("- '$/clear' to clear your script and the console.");
				return true;
		}

		Console.WriteLine("Unknown command. Check your spelling.");
		return true;
	}
}