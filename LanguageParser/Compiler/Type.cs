using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public abstract class Type : IEquatable<Type>
{
	public virtual bool Public => false;
	public LLVMTypeRef LlvmType { get; }
	public abstract ReadOnlyMemory<char> Name { get; }
	public static implicit operator LLVMTypeRef(Type t) => t.LlvmType;

	private PointerType? _pointerType;
	private PointerType? _constPointerType;
	internal readonly CompilationContext CompilationContext;
	
	internal readonly ulong MetadataTableOffset;
	internal LLVMValueRef LlvmMetadataTableOffset => LLVMValueRef.CreateConstInt(
		CompilationContext.LlvmContext.Int64Type, MetadataTableOffset
	);

	internal Type(CompilationContext context, LLVMTypeRef llvmType)
	{
		LlvmType = llvmType;
		CompilationContext = context;
		MetadataTableOffset = context.RegisterTypeForReflection(this);
	}

	public PointerType MakePointer(bool @const = false)
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

public sealed class IntrinsicType : Type
{
	public override bool Public => false;
	public override ReadOnlyMemory<char> Name { get; }

	public IntrinsicType(CompilationContext context, string name, LLVMTypeRef llvmType) 
		: this(context, name.AsMemory(), llvmType) {}

	public IntrinsicType(
		CompilationContext context, 
		ReadOnlyMemory<char> name, 
		LLVMTypeRef llvmType
	) : base(context, llvmType)
	{
		Name = name;
	}
}

public sealed class PointerType : Type
{
	public Type Base { get; }
	public bool Constant { get; }
	public override ReadOnlyMemory<char> Name { get; }

	public PointerType(Type @base, bool @const) 
		: base(@base.CompilationContext, LLVMTypeRef.CreatePointer(@base, 0))
	{
		Base = @base;
		Constant = @const;
		Name = $"{@base.Name}*".AsMemory();
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
	public required IReadOnlyDictionary<ReadOnlyMemory<char>, TypeMember> Members { get; init; }

	public StructType(CompilationContext context, ReadOnlyMemory<char> name, LLVMTypeRef llvmType)
		: base(context, llvmType) => Name = name;

	internal static StructType Create(
		CompilationContext context, 
		ReadOnlyMemory<char> name, 
		IReadOnlyList<(ReadOnlyMemory<char>, Type)> members
	)
	{
		var llvmType = context.LlvmContext.CreateNamedStruct(name.Span);
		Span<LLVMTypeRef> memberTypes = stackalloc LLVMTypeRef[members.Count];
		var memberDictionary = new Dictionary<ReadOnlyMemory<char>, TypeMember>(MemoryStringComparer.Instance);
		for (var i = 0; i < members.Count; i++)
		{
			var (memberName, memberType) = members[i];
			memberTypes[i] = memberType.LlvmType;
			memberDictionary.Add(memberName, new TypeMember { Idx = (uint) i, Name = memberName, Type = memberType });
		}
		
		llvmType.StructSetBody(memberTypes, false);
		return new StructType(context, name, llvmType) { Members = memberDictionary };
	}
}

public sealed class FunctionType : Type
{
	public bool Variadic { get; }
	public Type ReturnType { get; }
	public IReadOnlyList<Type> ParameterTypes { get; }
	public override ReadOnlyMemory<char> Name { get; }

	public FunctionType(Type returnType, IReadOnlyList<Type> parameterTypes, bool variadic) 
		: base(returnType.CompilationContext, MakeLlvmType(returnType, parameterTypes, variadic))
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