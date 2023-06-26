namespace LanguageParser;

public sealed class MemoryStringComparer : IEqualityComparer<ReadOnlyMemory<char>>
{
	public static readonly MemoryStringComparer Instance = new();

	public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
		=> x.Span.SequenceEqual(y.Span);

	public int GetHashCode(ReadOnlyMemory<char> obj)
	{
		var hash = new HashCode();
		foreach (var c in obj.Span) hash.Add(c);
		return hash.ToHashCode();
	}
}