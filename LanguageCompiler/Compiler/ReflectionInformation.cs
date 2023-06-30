namespace Squyrm.Compiler;

public sealed partial class CompilationContext : IDisposable
{
	private const string MetadataTableFn = "__MetadataTable<fn>__";
	private const string MetadataTableTy = "__MetadataTable<ty>__";
	
	private readonly Dictionary<LLVMTypeRef, (ulong, Type)> _types = new();
	internal readonly Dictionary<ReadOnlyMemory<char>, Value> Metadata = new(MemoryStringComparer.Instance);

	internal ulong RegisterTypeForReflection(Type type)
	{
		if (!CompilationSettings.EmitReflectionInformation) return 0;
		
		if (_types.TryGetValue(type.LlvmType, out var result))
			return result.Item1;
		
		var offset = (ulong) _types.Count;
		_types.Add(type.LlvmType, (offset, type));
		return offset;
	}

	private void InitializeReflectionInformation()
	{
		if (!CompilationSettings.EmitReflectionInformation) return;
		var i64 = DefaultTypes["i64".AsMemory()];
		
		InitializeReflectionTypes(
			out var tablesT, 
			out var tyTableRefT,
			out var fnTableRefT
		);

		LLVMValueRef tables;
		{
			tables = LlvmModule.AddGlobal(tablesT, "__MetadataTables__");
			tables.Linkage = LLVMLinkage.LLVMPrivateLinkage;
		}
		{
			var index = LLVMValueRef.CreateConstInt(i64, tablesT.Members["types".AsMemory()].Idx);
			var gep = LLVMValueRef.CreateConstGEP2(tyTableRefT, tables, new ReadOnlySpan<LLVMValueRef>(index));
			Metadata.Add(MetadataTableTy.AsMemory(), new Value(gep, tyTableRefT.MakePointer(true)));
		}
		{
			var index = LLVMValueRef.CreateConstInt(i64, tablesT.Members["functions".AsMemory()].Idx);
			var gep = LLVMValueRef.CreateConstGEP2(fnTableRefT, tables, new ReadOnlySpan<LLVMValueRef>(index));
			Metadata.Add(MetadataTableFn.AsMemory(), new Value(gep, fnTableRefT.MakePointer(true)));
		}
		
		Metadata.Add(
			"__ModuleName__".AsMemory(), 
			MakeConstString(CompilationSettings.ModuleName.AsMemory(), LLVMUnnamedAddr.LLVMLocalUnnamedAddr)
		);
	}

	private void InitializeReflectionTypes(out StructType tables, out StructType tyTableRef, out StructType fnTableRef)
	{
		var i8Ptr = DefaultTypes["i8".AsMemory()].MakePointer();

		StructType typeMetadataT;
		{
			var name = "__TypeMetadata__".AsMemory();
			typeMetadataT = StructType.Create(this, name, new[]
			{
				("name".AsMemory(), (Type) i8Ptr),
			});
			DefaultTypes.Add(name, typeMetadataT);
			tyTableRef = MakeMetadataTableRef(typeMetadataT);
		}
		
		{
			var name = "__FunctionMetadata__".AsMemory();
			var type = StructType.Create(this, name, new[]
			{
				("ptr".AsMemory(), i8Ptr),
				("name".AsMemory(), i8Ptr),
				("type".AsMemory(), (Type) typeMetadataT.MakePointer()),
			});
			DefaultTypes.Add(name, type);
			fnTableRef = MakeMetadataTableRef(type);
		}

		{
			var name = "__MetadataTables__".AsMemory();
			var type = LlvmContext.CreateNamedStruct(name.Span);
			type.StructSetBody(new LLVMTypeRef[] { fnTableRef }, false);

			tables = StructType.Create(this, name, new []
			{
				("types".AsMemory(), (Type) tyTableRef),
				("functions".AsMemory(), (Type) fnTableRef),
			});
			DefaultTypes.Add(name, tables);
		}
	}

	private void FinalizeReflectionInformation()
	{
		if (!CompilationSettings.EmitReflectionInformation) return;
		var stats = new RuntimeStats();

		var i8Ptr = DefaultTypes["i8".AsMemory()].MakePointer();
		var tablesT = (StructType) DefaultTypes["__MetadataTables__".AsMemory()];
		var tableRefValues = new LLVMValueRef[tablesT.Members.Count];

		(LLVMValueRef, LLVMTypeRef) PopulateMetadataTable(
			string metadataTypeName, 
			Func<Type, LLVMValueRef[]> makeValues, 
			string name, string globalName
		)
		{
			var metadataT = DefaultTypes[metadataTypeName.AsMemory()];
			var values = makeValues(metadataT);
			
			var global = LlvmModule.AddGlobal(LLVMTypeRef.CreateArray(metadataT, (uint) values.Length), globalName);
			global.Initializer = LLVMValueRef.CreateConstArray(metadataT, values);
			global.Linkage = LLVMLinkage.LLVMPrivateLinkage;
			
			var index = tablesT.Members[name.AsMemory()].Idx;
			tableRefValues[index] = LLVMValueRef.CreateConstNamedStruct(MakeMetadataTableRef(metadataT), new[]
			{
				LLVMValueRef.CreateConstBitCast(global, i8Ptr),
				LLVMValueRef.CreateConstInt(LlvmContext.Int64Type, (ulong) values.Length),
			});

			return (global, metadataT);
		}
		
		var (types, typeT) = PopulateMetadataTable("__TypeMetadata__", type =>
		{
			return _types.Values
				.Select(t => LLVMValueRef.CreateConstNamedStruct(type, stackalloc LLVMValueRef[]
				{
					MakeConstString(t.Item2.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr).LlvmValue,
				}))
				.ToArray();
		}, "types", MetadataTableTy);
		
		var (functions, functionT) = PopulateMetadataTable("__FunctionMetadata__", type =>
		{
			return Namespaces.Values
				.SelectMany(ns => ns.Functions.Values)
				.Select(f => LLVMValueRef.CreateConstNamedStruct(type, stackalloc LLVMValueRef[]
				{
					LLVMValueRef.CreateConstBitCast(f.LlvmValue, i8Ptr),
					MakeConstString(f.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr).LlvmValue,
					LLVMValueRef.CreateConstGEP2(typeT, types, new ReadOnlySpan<LLVMValueRef>(f.Type.LlvmMetadataTableOffset)),
				}))
				.ToArray();
		}, "functions", MetadataTableFn);

		var tablesGlobal = LlvmModule.GetNamedGlobal("__MetadataTables__");
		tablesGlobal.Initializer = LLVMValueRef.CreateConstNamedStruct(tablesT, tableRefValues);
		stats.Dump("Reflection metadata generation", ConsoleColor.Blue);
	}

	private StructType MakeMetadataTableRef(Type elementType)
	{
		var name = $"__MetadataTableRef<{elementType.Name}>__".AsMemory();
		if (DefaultTypes.TryGetValue(name, out var type)) return (StructType) type;
		
		var i64 = DefaultTypes["i64".AsMemory()];
		var elementPtr = elementType.MakePointer();

		type = StructType.Create(this, name, new[]
		{
			("ptr".AsMemory(), elementPtr),
			("len".AsMemory(), i64),
		});
		DefaultTypes.Add(name, type);

		return (StructType) type;
	}
}