using Pastel;

namespace Squyrm.Compiler;

public sealed class CompilationException : Exception
{
	public CompilationException(string message) : base(message) {}
	public CompilationException(string message, Exception? innerException) : base(message, innerException) {}

	public void Dump(ConsoleColor color)
	{
		Console.WriteLine(Message.Pastel(color));
		switch (InnerException)
		{
			case CompilationException e:
				e.Dump(color);
				break;
			
			case UnexpectedTokenException e:
				Console.WriteLine(e.Message.Pastel(color));
				break;
			
			case {} e:
				Console.WriteLine(e.ToString().Pastel(color));
				break;
			
			case null:
				return;
		}
	}
}