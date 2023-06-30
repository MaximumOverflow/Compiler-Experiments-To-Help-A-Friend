using System.Diagnostics;

namespace Squyrm.Compiler.Compiler.Passes;

public static class ReflectionGenerationPass
{
	internal static ReflectionData Initialize(CompilationContext context)
	{
		if (context.ReflectionInfo is null)
			return default;
		
		var u64 = context.DefaultTypes["u64".AsMemory()];
		Type ptr = context.DefaultTypes["i8".AsMemory()].MakePointer(true);
		Type str = context.DefaultTypes["i8".AsMemory()].MakePointer(true);
		
		var typeT = StructType.Create(context, "__TypeMetadata__".AsMemory());
		context.DefaultTypes[typeT.Name] = typeT;
		
		var fieldT = StructType.Create(context, "__FieldMetadata__".AsMemory());
		context.DefaultTypes[fieldT.Name] = fieldT;
		
		var functionT = StructType.Create(context, "__FunctionMetadata__".AsMemory());
		context.DefaultTypes[functionT.Name] = functionT;
		
		var typesRefT = MakeTableRefType(context, typeT);
		var fieldsRefT = MakeTableRefType(context, fieldT);
		var functionsRefT = MakeTableRefType(context, functionT);
		
		typeT.SetBody(new[]
		{
			("name".AsMemory(), str),
			("size".AsMemory(), u64),
			("alignment".AsMemory(), u64),
			("fields".AsMemory(), fieldsRefT),
		});
		fieldT.SetBody(new[]
		{
			("name".AsMemory(), str),
			("type".AsMemory(), typeT.MakePointer(true)),
		});
		
		functionT.SetBody(new[]
		{
			("name".AsMemory(), str),
			("type".AsMemory(), typeT.MakePointer(true)),
			("ptr".AsMemory(), ptr),
		});
		
		var tablesT = StructType.Create(context, "__MetadataTables__".AsMemory(), new(ReadOnlyMemory<char>, Type)[]
		{
			("types".AsMemory(), typesRefT),
			("fields".AsMemory(), fieldsRefT),
			("functions".AsMemory(), functionsRefT),
		});
		context.DefaultTypes[tablesT.Name] = tablesT;
		
		var tablesGlobal = context.LlvmModule.AddGlobal(tablesT, tablesT.Name.Span);
		tablesGlobal.Initializer = LLVMValueRef.CreateConstNull(tablesT);
		var tables = new Value(tablesGlobal, tablesT.MakePointer(true));
		context.ReflectionInfo.MetadataValues[tablesT.Name] = tables;

		return new ReflectionData
		{
			UInt64T = u64,
			VoidPtrT = ptr,
			
			TypeT = typeT,
			FieldT = fieldT,
			FunctionT = functionT,
			
			TablesT = tablesT,
			TypeTableRefT = typesRefT,
			FieldTableRefT = fieldsRefT,
			FunctionTableRefT = functionsRefT,
			
			MetadataTables = tables,
			ReflectionInfo = context.ReflectionInfo,
		};
	}

	internal static void Execute
	(
		CompilationContext context,
		ReflectionData rd
	)
	{
		if(context.ReflectionInfo is null)
			return;
		
		Value CreateTable(StructType elementT, uint count)
		{
			var name = $"__MetadataTable<{elementT.Name}>__";
			var type = LLVMTypeRef.CreateArray(elementT, count);
			var global = context.LlvmModule.AddGlobal(type, name);
			global.Initializer = LLVMValueRef.CreateConstNull(type);
			return new(global, elementT.MakePointer(true));
		}

		var zero = LLVMValueRef.CreateConstInt(rd.UInt64T, 0);

		var tables = rd.MetadataTables.LlvmValue;
		var fields = CreateTable(rd.FieldT, rd.ReflectionInfo.TypeFieldCount).LlvmValue;
		var types = CreateTable(rd.TypeT, (uint) rd.ReflectionInfo.Types.Count).LlvmValue;
		var functions = CreateTable(rd.FunctionT, (uint) rd.ReflectionInfo.Functions.Count).LlvmValue;

		// Initialize type and field tables
		{
			var typeT = rd.TypeT;
			var fieldT = rd.FieldT;
			var fieldTableRefT = rd.FieldTableRefT;

			var typeInstantiation = new LLVMValueRef[rd.TypeT.Fields.Count];
			var fieldInstantiation = new LLVMValueRef[rd.FieldT.Fields.Count];
			
			var typeValues = new LLVMValueRef[rd.ReflectionInfo.Types.Count];
			var fieldValues = new List<LLVMValueRef>((int)rd.ReflectionInfo.TypeFieldCount);
			
			foreach (var type in rd.ReflectionInfo.Types.Keys)
			{
				typeInstantiation[0] = context.MakeConstString(type.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr);
				typeInstantiation[1] = type.LlvmType.IsSized ? type.LlvmType.SizeOf : zero;
				typeInstantiation[2] = type.LlvmType.IsSized ? type.LlvmType.AlignOf : zero;

				if (type is StructType { Fields: var typeFields })
				{
					typeInstantiation[3] = MakeTableRef(fieldT, fieldTableRefT, fields, (uint) fieldValues.Count, (uint) typeFields.Count);
					foreach (var field in typeFields.Values)
					{
						fieldInstantiation[0] = context.MakeConstString(field.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr);
						fieldInstantiation[1] = LLVMValueRef.CreateConstGEP2(
							typeT, types,
							new ReadOnlySpan<LLVMValueRef>(LLVMValueRef.CreateConstInt(context.LlvmContext.Int64Type,
								field.Type.MetadataTableOffset))
						);
						
						CheckForNullValues(fieldInstantiation);
						fieldValues.Add(LLVMValueRef.CreateConstNamedStruct(fieldT, fieldInstantiation));
					}
				}
				else
				{
					typeInstantiation[3] = LLVMValueRef.CreateConstNull(fieldTableRefT);
				}

				CheckForNullValues(typeInstantiation);
				typeValues[type.MetadataTableOffset] = LLVMValueRef.CreateConstNamedStruct(typeT, typeInstantiation);
			}

			CheckForNullValues(typeValues);
			CheckForNullValues(CollectionsMarshal.AsSpan(fieldValues));
			types.Initializer = LLVMValueRef.CreateConstArray(typeT, typeValues);
			fields.Initializer = LLVMValueRef.CreateConstArray(fieldT, CollectionsMarshal.AsSpan(fieldValues));
		}

		// Initialize function table
		{
			var typeT = rd.TypeT;
			var functionT = rd.FunctionT;
			
			var funcInstantiation = new LLVMValueRef[rd.FunctionT.Fields.Count];
			var funcValues = new LLVMValueRef[rd.ReflectionInfo.Functions.Count];
			
			foreach (var func in rd.ReflectionInfo.Functions.Keys)
			{
				funcInstantiation[0] = context.MakeConstString(func.Name, LLVMUnnamedAddr.LLVMLocalUnnamedAddr);
				funcInstantiation[1] = LLVMValueRef.CreateConstGEP2(
					typeT, types,
					new ReadOnlySpan<LLVMValueRef>(LLVMValueRef.CreateConstInt(context.LlvmContext.Int64Type,
						func.Type.MetadataTableOffset))
				);
				funcInstantiation[2] = func.LlvmValue;
				
				CheckForNullValues(funcInstantiation);
				funcValues[func.MetadataTableOffset] = LLVMValueRef.CreateConstNamedStruct(functionT, funcInstantiation);
			}
			
			CheckForNullValues(funcValues);
			functions.Initializer = LLVMValueRef.CreateConstArray(functionT, funcValues);
		}
		
		tables.Initializer = LLVMValueRef.CreateConstNamedStruct(rd.TablesT, stackalloc LLVMValueRef[]
		{
			MakeTableRef(rd.TypeT, rd.TypeTableRefT, types, 0, (ulong) rd.ReflectionInfo.Types.Count),
			MakeTableRef(rd.FieldT, rd.FieldTableRefT, fields, 0, rd.ReflectionInfo.TypeFieldCount),
			MakeTableRef(rd.FunctionT, rd.FunctionTableRefT, functions, 0, (ulong) rd.ReflectionInfo.Functions.Count),
		});
	}

	private static StructType MakeTableRefType(CompilationContext context, StructType tableElementT)
	{
		var name = $"__MetadataTableRef<{tableElementT.Name}>__";
		if (context.DefaultTypes.TryGetValue(name.AsMemory(), out var existing))
			return (StructType) existing;
			
		var i64 = context.DefaultTypes["i64".AsMemory()];
		var tableRefT = StructType.Create(context, name.AsMemory(), new[]
		{
			("ptr".AsMemory(), tableElementT.MakePointer(true)),
			("len".AsMemory(), i64),
		});
		context.DefaultTypes[tableRefT.Name] = tableRefT;
		return tableRefT;
	}

	private static LLVMValueRef MakeTableRef(StructType elementT, StructType tableRefT, LLVMValueRef table, ulong offset, ulong length)
	{
		Span<LLVMValueRef> values = stackalloc LLVMValueRef[]
		{
			LLVMValueRef.CreateConstGEP2(
				elementT, table,
				new ReadOnlySpan<LLVMValueRef>(LLVMValueRef.CreateConstInt(table.TypeOf.Context.Int64Type, offset))
			),
			LLVMValueRef.CreateConstInt(table.TypeOf.Context.Int64Type, length),
		};
		
		CheckForNullValues(values);
		return LLVMValueRef.CreateConstNamedStruct(tableRefT, values);
	}

	[Conditional("DEBUG")]
	private static void CheckForNullValues(Span<LLVMValueRef> values)
	{
		for (var i = 0; i < values.Length; i++)
		{
			if(values[i] == default)
				throw new NullReferenceException($"One or more type fields are null. Null value at position {i}.");	
		}
	}

	internal readonly struct ReflectionData
	{
		public required Type UInt64T { get; init; }
		public required Type VoidPtrT { get; init; }
		
		public required StructType TypeT { get; init; }
		public required StructType FieldT { get; init; }
		public required StructType FunctionT { get; init; }
		
		public required StructType TablesT { get; init; }
		public required StructType TypeTableRefT { get; init; }
		public required StructType FieldTableRefT { get; init; }
		public required StructType FunctionTableRefT { get; init; }
		
		public required Value MetadataTables { get; init; }
		public required ReflectionInfo ReflectionInfo { get; init; }
	}

	public sealed class ReflectionInfo
	{
		internal uint TypeFieldCount { get; private set; }
		internal readonly Dictionary<Type, ulong> Types;
		internal readonly Dictionary<Function, ulong> Functions;
		internal readonly Dictionary<ReadOnlyMemory<char>, Value> MetadataValues;

		internal ReflectionInfo()
		{
			Types = new Dictionary<Type, ulong>();
			Functions = new Dictionary<Function, ulong>();
			MetadataValues = new Dictionary<ReadOnlyMemory<char>, Value>(MemoryStringComparer.Instance);
		}

		internal ulong RegisterType(Type type)
		{
			if(!RegisterIndex(type, Types, out var index) && type is StructType { Fields: var fields })
				TypeFieldCount += (uint) fields.Count;

			return index;
		}

		internal void UpdateTypeFieldCount(StructType type, uint previousFieldCount)
		{
			TypeFieldCount -= previousFieldCount;
			TypeFieldCount += (uint) type.Fields.Count;
		}
		
		internal ulong RegisterFunction(Function function)
		{
			RegisterIndex(function, Functions, out var index);
			return index;
		}

		private static bool RegisterIndex<T>(T element, Dictionary<T, ulong> indices, out ulong index) where T : notnull
		{
			if (indices.TryGetValue(element, out index)) 
				return false;
			
			index = (ulong) indices.Count;
			indices.Add(element, index);
			return true;
		}
	}
}