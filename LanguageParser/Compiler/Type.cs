using System.Collections.Immutable;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public sealed class Type
{
	public Type? Base { get; init; }
	public required bool Public { get; init; }
	public required LLVMTypeRef LlvmType { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
	public required IReadOnlyDictionary<ReadOnlyMemory<char>, TypeMember> Members { get; init; }
	public static implicit operator LLVMTypeRef(Type t) => t.LlvmType;

	private Type? _pointerType;

	public Type MakePointer()
	{
		_pointerType ??= new Type
		{
			Base = this,
			Public = Public,
			Name = $"{Name}*".AsMemory(),
			LlvmType = LLVMTypeRef.CreatePointer(LlvmType, 0),
			Members = ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty,
		};
		
		return _pointerType;
	}
}

public readonly struct TypeMember
{
	public required uint Idx { get; init; }
	public required Type Type { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
}