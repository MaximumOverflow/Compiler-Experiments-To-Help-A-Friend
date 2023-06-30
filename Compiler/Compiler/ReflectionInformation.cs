namespace Squyrm.Compiler;

public sealed partial class CompilationContext : IDisposable
{
	private readonly Dictionary<Type, ulong> _types = new();
	internal readonly Dictionary<ReadOnlyMemory<char>, Value> Metadata = new(MemoryStringComparer.Instance);

	internal ulong RegisterTypeForReflection(Type type)
	{
		if (!CompilationSettings.EmitReflectionInformation) return 0;
		
		if (_types.TryGetValue(type, out var result))
			return result;
		
		var offset = (ulong) _types.Count;
		_types.Add(type, offset);
		return offset;
	}

	private void InitializeReflectionInformation()
	{
		if (!CompilationSettings.EmitReflectionInformation) return;
		
		var i64 = DefaultTypes["i64".AsMemory()];
		Type ptr = DefaultTypes["i8".AsMemory()].MakePointer(true);
		Type str = DefaultTypes["i8".AsMemory()].MakePointer(true);

		LLVMValueRef MakeNullTableRef(Type type)
		{
			var t = (StructType) type;
			return LLVMValueRef.CreateConstNamedStruct(type, stackalloc LLVMValueRef[]
			{
				LLVMValueRef.CreateConstPointerNull(t.Fields["ptr".AsMemory()].Type),
				LLVMValueRef.CreateConstInt(i64, 0),
			});
		}

		Type rangeT = StructType.Create(this, "__MetadataRange__".AsMemory(), new[]
		{
			("start".AsMemory(), i64),
			("end".AsMemory(), i64),
		});
		DefaultTypes[rangeT.Name] = rangeT;
		
		var typeT = StructType.Create(this, "__TypeMetadata__".AsMemory(), new[]
		{
			("name".AsMemory(), str),
			("fields".AsMemory(), rangeT),
		});
		DefaultTypes[typeT.Name] = typeT;

		var fieldT = StructType.Create(this, "__FieldMetadata__".AsMemory(), new[]
		{
			("name".AsMemory(), str),
			("type".AsMemory(), i64),
		});
		DefaultTypes[fieldT.Name] = fieldT;

		Type functionT = StructType.Create(this, "__FunctionMetadata__".AsMemory(), new[]
		{
			("name".AsMemory(), str),
			("type".AsMemory(), i64),
			("ptr".AsMemory(), ptr),
		});
		DefaultTypes[functionT.Name] = functionT;

		var typesRefT = MakeMetadataTableRef(typeT);
		var fieldsRefT = MakeMetadataTableRef(fieldT);
		var functionsRefT = MakeMetadataTableRef(functionT);
		var tablesT = StructType.Create(this, "__MetadataTables__".AsMemory(), new(ReadOnlyMemory<char>, Type)[]
		{
			("types".AsMemory(), typesRefT),
			("fields".AsMemory(), fieldsRefT),
			("functions".AsMemory(), functionsRefT),
		});
		DefaultTypes[tablesT.Name] = tablesT;
		
		var tables = LlvmModule.AddGlobal(tablesT, tablesT.Name.Span);
		var tableRefs = tablesT.Fields.Values.Select(m => MakeNullTableRef(m.Type)).ToArray();
		tables.Initializer = LLVMValueRef.CreateConstNamedStruct(tablesT, tableRefs);
		Metadata[tablesT.Name] = new(tables, tablesT.MakePointer(true));
	}

	private void FinalizeReflectionInformation()
	{
		if (!CompilationSettings.EmitReflectionInformation) return;

		Type ptrT = DefaultTypes["i8".AsMemory()].MakePointer(true);
		var rangeT = DefaultTypes["__MetadataRange__".AsMemory()];
		var tablesT = (StructType) DefaultTypes["__MetadataTables__".AsMemory()];

		Span<LLVMValueRef> llvmValues2 = stackalloc LLVMValueRef[2];
		Span<LLVMValueRef> llvmValues3 = stackalloc LLVMValueRef[3];
		Span<LLVMValueRef> tableRefs = stackalloc LLVMValueRef[tablesT.Fields.Count];
		
		LLVMValueRef InitializeTable(Type type, ReadOnlySpan<LLVMValueRef> values)
		{
			var initializer = LLVMValueRef.CreateConstArray(type, values);
			var global = LlvmModule.AddGlobal(initializer.TypeOf, type.Name.Span);
			global.Initializer = initializer;
			Metadata[type.Name] = new Value(global, type.MakePointer(true));

			var tableRefT = MakeMetadataTableRef(type);
			return LLVMValueRef.CreateConstNamedStruct(tableRefT, stackalloc LLVMValueRef[]
			{
				LLVMValueRef.CreateConstPointerCast(global, tableRefT.Fields["ptr".AsMemory()].Type),
				LLVMValueRef.CreateConstInt(LlvmContext.Int64Type, (ulong) values.Length),
			});
		}

		{
			var types = new List<LLVMValueRef>(_types.Count);
			var fields = new List<LLVMValueRef>(_types.Count);
			var typeT = DefaultTypes["__TypeMetadata__".AsMemory()];
			var fieldT = DefaultTypes["__FieldMetadata__".AsMemory()];

			foreach (var (type, _) in _types)
			{
				if (type is StructType t)
				{
					llvmValues2[0] = LLVMValueRef.CreateConstInt(LlvmContext.Int64Type, (ulong) fields.Count);
					llvmValues2[1] = LLVMValueRef.CreateConstInt(LlvmContext.Int64Type, (ulong) (fields.Count + t.Fields.Count));
					llvmValues2[1] = LLVMValueRef.CreateConstNamedStruct(rangeT, llvmValues2);
					llvmValues2[0] = MakeConstString(type.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr).LlvmValue;
					types.Add(LLVMValueRef.CreateConstNamedStruct(typeT, llvmValues2));
					
					foreach (var field in t.Fields.Values)
					{
						llvmValues2[0] = MakeConstString(field.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr).LlvmValue;
						llvmValues2[1] = field.Type.LlvmMetadataTableOffset;
						fields.Add(LLVMValueRef.CreateConstNamedStruct(fieldT, llvmValues2));
					}
				}
				else
				{
					llvmValues2[0] = llvmValues2[1] = LLVMValueRef.CreateConstInt(LlvmContext.Int64Type, 0);
					llvmValues2[1] = LLVMValueRef.CreateConstNamedStruct(rangeT, llvmValues2);
					llvmValues2[0] = MakeConstString(type.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr).LlvmValue;
					types.Add(LLVMValueRef.CreateConstNamedStruct(typeT, llvmValues2));
				}
			}

			tableRefs[0] = InitializeTable(typeT, CollectionsMarshal.AsSpan(types));
			tableRefs[1] = InitializeTable(fieldT, CollectionsMarshal.AsSpan(fields));
		}

		{
			var functions = new List<LLVMValueRef>();
			var functionT = DefaultTypes["__FunctionMetadata__".AsMemory()];

			foreach (var fn in Namespaces.Values.SelectMany(ns => ns.Functions.Values))
			{
				llvmValues3[0] = MakeConstString(fn.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr).LlvmValue;
				llvmValues3[1] = fn.Type.LlvmMetadataTableOffset;
				llvmValues3[2] = LLVMValueRef.CreateConstPointerCast(fn.LlvmValue, ptrT);
				functions.Add(LLVMValueRef.CreateConstNamedStruct(functionT, llvmValues3));
			}

			tableRefs[2] = InitializeTable(functionT, CollectionsMarshal.AsSpan(functions));
		}
		
		var tables = Metadata[tablesT.Name].LlvmValue;
		tables.Initializer = LLVMValueRef.CreateConstNamedStruct(tablesT, tableRefs);
	}
	
	private StructType MakeMetadataTableRef(Type type)
	{
		var name = $"__MetadataTableRef<{type.Name}>__";
		if (DefaultTypes.TryGetValue(name.AsMemory(), out var existing))
			return (StructType) existing;
			
		var i64 = DefaultTypes["i64".AsMemory()];
		var tableRefT = StructType.Create(this, name.AsMemory(), new[]
		{
			("ptr".AsMemory(), type.MakePointer(true)),
			("len".AsMemory(), i64),
		});
		DefaultTypes[tableRefT.Name] = tableRefT;
		return tableRefT;
	}
}