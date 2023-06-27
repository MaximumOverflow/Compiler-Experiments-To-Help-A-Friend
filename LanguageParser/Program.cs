using System.Text.RegularExpressions;
using LanguageParser.Tokenizer;
using LanguageParser.Compiler;
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
            new Program().Run();
        }

        private void Run()
        {
            Console.WriteLine("Welcome to the Storm shell.");
            Console.WriteLine("Enter '$/help' to get a list of commands.");

            while (_isRunning)
            {
                Console.Write("|>> ");
                var input = Console.ReadLine() ?? string.Empty;

                if (ProcessCommand(input))
                {
                    
                }
                else
                {
                    _script.Append(input);
                    _script.Append('\n');
                }
            }
        }

        private bool ProcessCommand(string input)
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

            if (input.StartsWith("$/run"))
            {
                try
                {
                    using var context = new CompilationContext("test");
                    var source = input.StartsWith("$/run @") 
                        ? File.ReadAllText(input[(input.IndexOf("@", StringComparison.Ordinal) + 1)..]) 
                        : _script.ToString();
	            
                    context.CompileSourceFile(source);
                    context.LlvmModule.Dump();
                    context.LlvmModule.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
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