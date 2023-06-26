using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public sealed class Type
{
	public required LLVMTypeRef LlvmType { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required IReadOnlyDictionary<ReadOnlyMemory<char>, TypeMember> Members { get; init; }
	public static implicit operator LLVMTypeRef(Type t) => t.LlvmType;
}

public readonly struct TypeMember
{
	public required uint Idx { get; init; }
	public required Type Type { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
}