using System.Collections.Immutable;
using ReflectionInfo = Squyrm.Compiler.Compiler.Passes.ReflectionGenerationPass.ReflectionInfo;

namespace Squyrm.Compiler;

public abstract class Type : IEquatable<Type>
{
	public virtual bool Public => false;
	public LLVMTypeRef LlvmType { get; }
	public abstract ReadOnlyMemory<char> Name { get; }
	public static implicit operator LLVMTypeRef(Type t) => t.LlvmType;

	private PointerType? _pointerType;
	private PointerType? _constPointerType;
	internal readonly ulong MetadataTableOffset;
	protected internal readonly ReflectionInfo? Reflection;

	internal Type(LLVMTypeRef llvmType, ReflectionInfo? reflection)
	{
		LlvmType = llvmType;
		Reflection = reflection;
		MetadataTableOffset = reflection?.RegisterType(this) ?? 0;
	}

	public PointerType MakePointer(bool @const)
	{
		if (@const)
		{
			_constPointerType ??= new PointerType(this, true);
			return _constPointerType;
		}
		else
		{
			_pointerType ??= new PointerType(this, false);
			return _pointerType;
		}
	}

	public virtual bool IsCompatibleWith(Type other)
	{
		return false;
	}

	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((Type) obj);
	}

	public virtual bool Equals(Type? other)
	{
		if (ReferenceEquals(null, other)) return false;
		if (other is PointerType) return other.Equals(this);
		if (other is FunctionType) return other.Equals(this);
		return LlvmType == other.LlvmType;
	}
	
	public override int GetHashCode()
	{
		return LlvmType.GetHashCode();
	}

	public static bool operator ==(Type? a, Type? b)
	{
		if (ReferenceEquals(a, b)) return true;
		return a is not null && a.Equals(b);
	}

	public static bool operator !=(Type? a, Type? b)
	{
		return !(a == b);
	}

	public override string ToString()
	{
		return Name.ToString();
	}
}

public class IntrinsicType : Type
{
	public override bool Public => false;
	public override ReadOnlyMemory<char> Name { get; }

	internal IntrinsicType(string name, LLVMTypeRef llvmType, ReflectionInfo? reflection) 
		: this(name.AsMemory(), llvmType, reflection) {}

	internal IntrinsicType(ReadOnlyMemory<char> name, LLVMTypeRef llvmType, ReflectionInfo? reflection) : base(llvmType, reflection)
	{
		Name = name;
	}
}

public sealed class IntegerType : IntrinsicType
{
	public uint Bits { get; }
	public bool Unsigned { get; }
	
	internal IntegerType(LLVMContextRef ctx, uint bits, bool unsigned, ReflectionInfo? reflection) 
		: base(MakeName(bits, unsigned), ctx.GetIntType(bits), reflection)
	{
		Bits = bits;
		Unsigned = unsigned;
	}

	private static ReadOnlyMemory<char> MakeName(uint bits, bool unsigned)
	{
		return $"{(unsigned ? 'u' : 'i')}{bits}".AsMemory();
	}
}

public sealed class PointerType : Type
{
	public Type Base { get; }
	public bool Constant { get; }
	public override ReadOnlyMemory<char> Name { get; }

	internal PointerType(Type @base, bool @const) 
		: base(LLVMTypeRef.CreatePointer(@base, 0), @base.Reflection)
	{
		Base = @base;
		Constant = @const;
		Name = $"*{(@const ? "unrelenting " : "")}{@base.Name}".AsMemory();
	}

	public override bool IsCompatibleWith(Type other)
	{
		if (other is not PointerType { LlvmType: var llvmType, Constant: var @const })
			return false;

		if (LlvmType != llvmType)
			return false;

		return (Constant, @const) switch
		{
			(true, false) => false,
			_ => true,
		};
	}

	public override bool Equals(object? obj)
	{
		return ReferenceEquals(this, obj) ||
		       (obj is PointerType ptr && ptr.LlvmType == LlvmType && ptr.Constant == Constant);
	}

	public override bool Equals(Type? other)
	{
		return ReferenceEquals(this, other) ||
		       (other is PointerType ptr && ptr.LlvmType == LlvmType && ptr.Constant == Constant);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(LlvmType, Constant);
	}
}

public sealed class StructType : Type
{
	public override ReadOnlyMemory<char> Name { get; }
	public IReadOnlyDictionary<ReadOnlyMemory<char>, TypeMember> Fields { get; private set; }

	private StructType(
		ReadOnlyMemory<char> name,
		LLVMTypeRef llvmType, 
		IReadOnlyDictionary<ReadOnlyMemory<char>, TypeMember> fields,
		ReflectionInfo? reflection
	) : base(llvmType, reflection)
	{
		Name = name;
		Fields = fields;
	}
	
	internal static StructType Create(CompilationContext context, ReadOnlyMemory<char> name)
	{
		var llvmType = context.LlvmContext.CreateNamedStruct(name.Span);
		return new StructType(name, llvmType, ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty, context.ReflectionInfo);
	}

	internal static StructType Create(
		CompilationContext context, 
		ReadOnlyMemory<char> name, 
		IReadOnlyList<(ReadOnlyMemory<char>, Type)> members
	)
	{
		var llvmType = context.LlvmContext.CreateNamedStruct(name.Span);
		var type = new StructType(name, llvmType, ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty, context.ReflectionInfo);
		
		if(members.Count != 0)
			type.SetBody(members);

		return type;
	}

	internal void SetBody(IReadOnlyList<(ReadOnlyMemory<char>, Type)> fields)
	{
		var prevFieldCount = (uint) Fields.Count;
		
		Span<LLVMTypeRef> fieldTypes = stackalloc LLVMTypeRef[fields.Count];
		var memberDictionary = new Dictionary<ReadOnlyMemory<char>, TypeMember>(MemoryStringComparer.Instance);
		for (var i = 0; i < fields.Count; i++)
		{
			var (fieldName, fieldType) = fields[i];
			fieldTypes[i] = fieldType.LlvmType;
			memberDictionary.Add(fieldName, new TypeMember { Idx = (uint) i, Name = fieldName, Type = fieldType });
		}

		Fields = memberDictionary;
		LlvmType.StructSetBody(fieldTypes, false);
		Reflection?.UpdateTypeFieldCount(this, prevFieldCount);
	}
}

public sealed class FunctionType : Type
{
	public bool Variadic { get; }
	public Type ReturnType { get; }
	public IReadOnlyList<Type> ParameterTypes { get; }
	public override ReadOnlyMemory<char> Name { get; }

	internal FunctionType(Type returnType, IReadOnlyList<Type> parameterTypes, bool variadic) 
		: base(MakeLlvmType(returnType, parameterTypes, variadic), returnType.Reflection)
	{
		Variadic = variadic;
		ReturnType = returnType;
		ParameterTypes = parameterTypes;
		Name = MakeName(returnType, parameterTypes, variadic).AsMemory();
	}

	private static LLVMTypeRef MakeLlvmType(Type returnType, IReadOnlyList<Type> parameterTypes, bool variadic)
	{
		Span<LLVMTypeRef> paramTypes = stackalloc LLVMTypeRef[parameterTypes.Count];
		for (var i = 0; i < parameterTypes.Count; i++) 
			paramTypes[i] = parameterTypes[i];
		
		return LLVMTypeRef.CreateFunction(returnType, paramTypes, variadic);
	}

	private static string MakeName(Type returnType, IEnumerable<Type> parameterTypes, bool variadic)
	{
		var retStr = returnType.Name;
		var paramsStr = string.Join(", ", parameterTypes.Select(t => t.Name));
		paramsStr = (variadic, paramsStr) switch
		{
			(true, "") => "...",
			(true, _) => $"{paramsStr}, ...",
			_ => paramsStr,
		};

		return $"{retStr} ({paramsStr})";
	}
}

public readonly struct TypeMember
{
	public required uint Idx { get; init; }
	public required Type Type { get; init; }
	public required ReadOnlyMemory<char> Name { get; init; }
}