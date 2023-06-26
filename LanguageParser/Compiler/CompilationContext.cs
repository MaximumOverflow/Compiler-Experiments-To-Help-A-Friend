using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Collections.Immutable;
using LanguageParser.Parser;
using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

public sealed class CompilationContext : IDisposable
{
	public LLVMModuleRef LlvmModule;
	public LLVMContextRef LlvmContext;
	public readonly Dictionary<ReadOnlyMemory<char>, Type> DefaultTypes;
	public readonly Dictionary<ReadOnlyMemory<char>, Namespace> Namespaces;

	public CompilationContext(string moduleName)
	{
		LlvmContext = LLVMContextRef.Create();
		LlvmModule = LlvmContext.CreateModuleWithName(moduleName);
		Namespaces = new Dictionary<ReadOnlyMemory<char>, Namespace>(MemoryStringComparer.Instance);
		DefaultTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance)
		{
			{
				"nothing".AsMemory(), new Type
				{
					Name = "nothing".AsMemory(),
					LlvmType = LlvmContext.VoidType,
					Members = ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty,
				}
			},
			
			{
				"rope".AsMemory(), new Type
				{
					Name = "rope".AsMemory(),
					LlvmType = LLVMTypeRef.CreatePointer(LlvmContext.Int8Type, 0),
					Members = ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty,
				}
			},
			
			{
				"i32".AsMemory(), new Type
				{
					Name = "i32".AsMemory(),
					LlvmType = LlvmContext.Int64Type,
					Members = ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty,
				}
			},
			
			{
				"i64".AsMemory(), new Type
				{
					Name = "i64".AsMemory(),
					LlvmType = LlvmContext.Int64Type,
					Members = ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty,
				}
			},

			{
				"f64".AsMemory(), new Type
				{
					Name = "f64".AsMemory(),
					LlvmType = LlvmContext.DoubleType,
					Members = ImmutableDictionary<ReadOnlyMemory<char>, TypeMember>.Empty,
				}
			},
		};
	}
	
	public void CompileSourceFile(string source)
	{
		var tokens = Tokenizer.Tokenizer.Tokenize(source);
		var stream = new TokenStream(CollectionsMarshal.AsSpan(tokens));
		
		if (!RootNode.TryParse(ref stream, out var root)) 
			throw new Exception("Failed to parse root node.");

		if(!Namespaces.TryGetValue(root.Namespace, out var @namespace))
			Namespaces.Add(root.Namespace, @namespace = new Namespace(root.Namespace));
		
		var context = new FileCompilationContext(this, @namespace);
		context.DefineTypes(root.Declarations);
		context.DefineFunctions(root.Declarations);
	}

	[SuppressMessage("ReSharper", "PossiblyImpureMethodCallOnReadonlyVariable")]
	public void Dispose()
	{
		LlvmModule.Dispose();
		LlvmContext.Dispose();
		GC.SuppressFinalize(this);
	}
	
	~CompilationContext() => Dispose();
}

internal sealed class FileCompilationContext
{
	public readonly CompilationContext GlobalContext;

	public readonly Namespace Namespace;
	public readonly Dictionary<ReadOnlyMemory<char>, Type> ImportedTypes;
	public readonly Dictionary<ReadOnlyMemory<char>, Function> ImportedFunctions;

	public FileCompilationContext(CompilationContext context, Namespace @namespace)
	{
		Namespace = @namespace;
		GlobalContext = context;
		ImportedTypes = new Dictionary<ReadOnlyMemory<char>, Type>(MemoryStringComparer.Instance);
		ImportedFunctions = new Dictionary<ReadOnlyMemory<char>, Function>(MemoryStringComparer.Instance);
	}

	public void DefineTypes(IReadOnlyList<IRootDeclarationNode> declarations)
	{
		foreach (var decl in declarations)
		{
			if(decl is not ClassNode @class) continue;
			var type = new Type
			{
				Name = @class.Name,
				LlvmType = GlobalContext.LlvmContext.CreateNamedStruct(@class.Name.Span),
				Members = new Dictionary<ReadOnlyMemory<char>, TypeMember>(@class.Members.Count, MemoryStringComparer.Instance),
			};
			Namespace.Types.Add(@class.Name, type);
		}

		foreach (var decl in declarations)
		{
			if(decl is not ClassNode @class) continue;
			var type = Namespace.Types[@class.Name];
			var memberTypes = new LLVMTypeRef[@class.Members.Count];
			var membersDict = (Dictionary<ReadOnlyMemory<char>, TypeMember>) type.Members;
			for (var i = 0; i < @class.Members.Count; i++)
			{
				var memberDef = @class.Members[i];
				var memberType = FindType(memberDef.Type.Name);
				
				memberTypes[i] = memberType;
				membersDict.Add(memberDef.Name, new TypeMember
				{
					Idx = (uint) i,
					Name = memberDef.Name,
					Type = memberType,
				});
			}
			
			type.LlvmType.StructSetBody(memberTypes, false);
		}
	}

	public void DefineFunctions(IReadOnlyList<IRootDeclarationNode> declarations)
	{
		foreach (var decl in declarations)
		{
			if(decl is not FunctionNode function) continue;
			
			var returnType = FindType(function.ReturnType.Name);
			var parameters = function.Parameters.Select(p => (p.Name, FindType(p.Type.Name))).ToArray();

			var paramTypes = parameters.Select(p => p.Item2.LlvmType).ToArray();
			var funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes);
			
			Namespace.Functions.Add(function.Name, new Function
			{
				Name = function.Name,
				ReturnType = returnType,
				LlvmValue = GlobalContext.LlvmModule.AddFunction(function.Name.Span, funcType),
				Parameters = parameters,
			});
		}

		foreach (var decl in declarations)
		{
			if (decl is not FunctionNode function) continue;
			var func = Namespace.Functions[function.Name];
			Function.SetBody(this, func, function.Block);
		}
	}

	public Type FindType(string typeName)
		=> FindType(typeName.AsMemory());

	public Type FindType(ReadOnlyMemory<char> typeName)
	{
		if (Namespace.Types.TryGetValue(typeName, out var type))
			return type;
		
		if (ImportedTypes.TryGetValue(typeName, out type))
			return type;

		if (GlobalContext.DefaultTypes.TryGetValue(typeName, out type))
			return type;

		throw new Exception($"Type '{typeName}' not found.");
	}
}