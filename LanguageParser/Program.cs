using System.Text.RegularExpressions;
using LanguageParser.Tokenizer;
using LanguageParser.Compiler;
using System.Diagnostics;
using LLVMSharp.Interop;
using System.Text;

namespace LanguageParser
{
    internal class Program
    {
        private bool _isRunning = true;
        private readonly StringBuilder _script = new();

        public static void Main()
        {
	        LLVM.LinkInMCJIT();
            LLVM.InitializeX86TargetInfo();
            LLVM.InitializeX86Target();
            LLVM.InitializeX86TargetMC();
            LLVM.InitializeX86AsmParser();
            LLVM.InitializeX86AsmPrinter();
            new Program().Run();
        }

        private void Run()
        {
            Console.WriteLine("Welcome to the Storm shell.");
            Console.WriteLine("Enter '$/help' to get a list of commands.");
            
            var compilationSettings = new CompilationSettings
            {
	            ModuleName = "Test",
	            OptimizationLevel = 0,
	            EmitReflectionInformation = false,
            };
            
            while (_isRunning)
            {
                Console.Write("|>> ");
                var input = Console.ReadLine() ?? string.Empty;
                if (!ProcessCommand(input, ref compilationSettings))
                {
                    _script.Append(input);
                    _script.Append('\n');
                }
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

	                using var context = new CompilationContext(settings);
	                context.CompileSourceFile(source);
	                context.FinalizeCompilation();

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

	                return false;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
	            return false;
            }

            switch (input)
            {
	            case "$/clear":
		            _script.Clear();
		            Console.Clear();
		            return false;
	            
	            case "$/help":
		            Console.WriteLine("- '$/exit' to leave the program.");
		            Console.WriteLine("- '$/run' to execute your script. " +
		                              "Adding a path like so '$/run @D:\\user\\scripts\\test.txt' will run the script within that file.");
		            Console.WriteLine("- '$/clear' to clear your script and the console.");
		            return false;
            }

            Console.WriteLine("Unknown command. Check your spelling.");
            return true;
        }

        private static void PrintTokens(List<Token> tokens, float delay)
        {
	        foreach (var token in tokens)
	        {
		        Console.WriteLine(token.ToString());
		        if(delay != 0) Thread.Sleep((int)(delay * 50));
	        }
        }

        private static void WriteBlock(string content, string regex, float delay)
        {
            var strings = Regex.Split(content, regex);
            foreach (var s in strings)
            {
                foreach (var c in s)
                {
                    Console.Write(c);
                    if(delay != 0) Thread.Sleep((int)(delay * 50));
                }

                Console.WriteLine();
            }
        }
    }
}