namespace LanguageParser.Compiler;

public sealed class Namespace
{
	public readonly ReadOnlyMemory<char> Name;
	public readonly Dictionary<ReadOnlyMemory<char>, Type> Types;
	public readonly Dictionary<ReadOnlyMemory<char>, Function> Functions;

	public Namespace(ReadOnlyMemory<char> name)
	{
		Name = name;
		Types = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance);
		Functions = new Dictionary<ReadOnlyMemory<char>, Function>(MemoryStringComparer.Instance);
	}
}