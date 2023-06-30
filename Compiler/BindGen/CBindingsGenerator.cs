using System.CodeDom.Compiler;
using ClangSharp.Interop;
using System.Text;
using ClangSharp;
using Pastel;

using Type = ClangSharp.Type;

namespace Squyrm.BindGen;

public static class CBindingsGenerator
{
	public static string GenerateSquyrmBindings(string filePath, string @namespace)
	{
		var translationUnit = TranslationUnit.GetOrCreate(
			CXTranslationUnit.Parse(
				CXIndex.Create(), filePath, 
				default, default, CXTranslationUnit_Flags.CXTranslationUnit_None
			)
		);

		var src = new StringBuilder();
		src.Append(@namespace);
		src.AppendLine(";\n");

		var declaredTypes = new Dictionary<string, string>();
		var declaredFunctions = new Dictionary<string, string>();
		foreach (var decl in translationUnit.TranslationUnitDecl.Decls)
		{
			try
			{
				switch (decl)
                {
                	case FunctionDecl {Name: var name, Visibility: var visibility} func:
                	{
	                    if(declaredFunctions.ContainsKey(name))
		                    continue;
	                    
	                    var stream = new StringWriter();
	                    var builder = new IndentedTextWriter(stream);
	                    
	                    builder.Write("foreign ");
	                    builder.Write(GetVisibilityString(name, visibility));
	                    builder.Write(' ');
	                    builder.WriteTypeString(func.ReturnType, declaredTypes);
	                    builder.Write(' ');
	                    builder.Write(name);
	                    builder.Write('(');
	                    {
		                    for (var i = 0; i < func.Parameters.Count; i++)
		                    {
			                    var param = func.Parameters[i];
			                    builder.WriteTypeString(param.Type, declaredTypes);
			                    builder.Write(' ');
			                    builder.Write(param.Name switch
			                    {
				                    "" or null => $"param{i}",
				                    _ => param.Name,
			                    });
			                    if (i != func.Parameters.Count - 1) builder.Write(", ");
		                    }
		                    builder.Write((func.NumParams, func.IsVariadic) switch
		                    {
			                    (0, true) => "...",
			                    (_, true) => ", ...",
			                    _ => "",
		                    });
	                    }
	                    builder.Write(");\n");
                		declaredFunctions.Add(name, stream.ToString());
                		break;
                	}
    
                	case RecordDecl {Name: var name } record:
                	{
	                    if(declaredTypes.ContainsKey(name)) 
		                    continue;
	                    
                        declaredTypes.TryAdd(name, MakeRecordTypeString(record, declaredTypes));
                		break;
                	}

                    case TypedefDecl
                    {
	                    Name: var name, 
	                    Visibility: var visibility,
	                    Kind: CX_DeclKind.CX_DeclKind_LastTypedefName,
                    } typeDef:
                    {
	                    if(declaredTypes.ContainsKey(name)) 
		                    continue;
	                    
	                    var fields = typeDef.TypeForDecl.AsTagDecl?.Decls;
	                    if(fields is null) continue;
	                    
	                    using var stream = new StringWriter();
	                    var builder = new IndentedTextWriter(stream);
	                    
	                    builder.Write(GetVisibilityString(name, visibility));
	                    builder.Write(' ');
	                    builder.Write("thing ");
	                    builder.Write(typeDef.Name);
	                    if (fields.Count != 0)
	                    {
		                    builder.WriteLine(" {");
		                    builder.Indent++;
		                    foreach (var field in fields)
		                    {
			                    switch (field)
			                    {
				                    case FieldDecl fieldDecl:
				                    {
					                    builder.Write(GetVisibilityString(fieldDecl.Name, fieldDecl.Visibility));
					                    builder.Write(' ');
					                    builder.WriteTypeString(fieldDecl.Type, declaredTypes);
					                    builder.Write(' ');
					                    builder.Write(fieldDecl.Name);
					                    builder.WriteLine(';');
					                    break;
				                    }
				                    
				                    default:
				                    {
					                    throw new NotSupportedException(
						                    $"Unsupported child declaration {field} of kind '{field.Kind}' for type '{name}' in file '{field.TranslationUnit.Handle}'."
					                    );
				                    }
			                    }
		                    }
		                    builder.Indent--;
	                    }
	                    else builder.Write(" {");
	                    builder.WriteLine('}');
	                    declaredTypes.TryAdd(name, stream.ToString());
	                    break;
                    }

                    default:
                    {
	                    throw new NotSupportedException(
		                    $"Unsupported type '{decl}' of kind '{decl.Kind}' in file '{decl.TranslationUnit.Handle}'."
		                );
                    }
                }
			}
			catch (NotSupportedException e)
			{
				Console.Error.WriteLine(e.Message.Pastel(ConsoleColor.Yellow));
			}
		}

		foreach (var decl in declaredTypes.Values) 
			src.AppendLine(decl);
		
		foreach (var decl in declaredFunctions.Values) 
			src.AppendLine(decl);

		return src.ToString();
	}

	private static string GetVisibilityString(string name, CXVisibilityKind visibility)
	{
		return visibility switch
		{
			CXVisibilityKind.CXVisibility_Hidden => "inaccessible",
			CXVisibilityKind.CXVisibility_Invalid => "inaccessible",
			CXVisibilityKind.CXVisibility_Protected => "inaccessible",
			CXVisibilityKind.CXVisibility_Default when name.StartsWith('_') => "inaccessible",
			CXVisibilityKind.CXVisibility_Default => "accessible",
			_ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null),
		};
	}

	private static void WriteTypeString(this IndentedTextWriter writer, Type type, Dictionary<string, string> declaredTypes)
	{
		while (true)
		{
			switch (type.Desugar)
			{
				case { Kind: CXTypeKind.CXType_Void }: writer.Write("nothing"); return;
				case { Kind: CXTypeKind.CXType_Bool }: writer.Write("maybe"); return;
				case { Kind: CXTypeKind.CXType_Int }: writer.Write("i32"); return;
				case { Kind: CXTypeKind.CXType_UInt }: writer.Write("u32"); return;
				case { Kind: CXTypeKind.CXType_Long }: writer.Write("i64"); return;
				case { Kind: CXTypeKind.CXType_ULong }: writer.Write("u64"); return;
				case { Kind: CXTypeKind.CXType_Char_S }: writer.Write("i8"); return;
				case { Kind: CXTypeKind.CXType_Char_U }: writer.Write("u8"); return;
				case { Kind: CXTypeKind.CXType_Short }: writer.Write("i16"); return;
				case { Kind: CXTypeKind.CXType_UShort }: writer.Write("u16"); return;
				case { Kind: CXTypeKind.CXType_LongLong }: writer.Write("i64"); return;
				case { Kind: CXTypeKind.CXType_ULongLong }: writer.Write("u64"); return;
				case { Kind: CXTypeKind.CXType_Int128 }: writer.Write("i128"); return;
				case { Kind: CXTypeKind.CXType_UInt128 }: writer.Write("u128"); return;
				case { Kind: CXTypeKind.CXType_Half }: writer.Write("f16"); return;
				case { Kind: CXTypeKind.CXType_Float }: writer.Write("f32"); return;
				case { Kind: CXTypeKind.CXType_Double }: writer.Write("f64"); return;
				case { Kind: CXTypeKind.CXType_LongDouble }: writer.Write("f128"); return;
				
				case PointerType {PointeeType.IsLocalConstQualified: true}:
					writer.Write("*unrelenting ");
					writer.WriteTypeString(type.PointeeType, declaredTypes);
					return;
					
				case PointerType {PointeeType.IsLocalConstQualified: false}:
					writer.Write('*');
					writer.WriteTypeString(type.PointeeType, declaredTypes);
					return;

				case TypedefType typeDef:
					type = typeDef.Desugar;
					continue;
				
				case ElaboratedType elaboratedType:
					type = elaboratedType.NamedType;
					continue;

				case RecordType recordType:
				{
					if (!declaredTypes.ContainsKey(recordType.Decl.Name)) goto default;
					writer.Write(recordType.Decl.Name);
					return;
				}

				default:
				{
					throw new NotSupportedException($"Unsupported type '{type}' of kind '{type.Kind}' in file '{type.TranslationUnit.Handle}'.");
				}
			}
		}
	}

	private static string MakeRecordTypeString(RecordDecl record, Dictionary<string, string> declaredTypes)
	{
		var stream = new StringWriter();
		var builder = new IndentedTextWriter(stream);
	                    
		builder.Write(GetVisibilityString(record.Name, record.Visibility));
		builder.Write(' ');
		builder.Write("thing ");
		builder.Write(record.Name);
		if (record.Fields.Count != 0)
		{
			builder.WriteLine(" {");
			builder.Indent++;
			foreach (var field in record.Fields)
			{
				builder.Write(GetVisibilityString(field.Name, field.Visibility));
				builder.Write(' ');
				builder.WriteTypeString(field.Type, declaredTypes);
				builder.Write(' ');
				builder.Write(field.Name);
				builder.WriteLine(';');
			}
			builder.Indent--;
		}
		else builder.Write(" {");
		builder.WriteLine('}');
		return stream.ToString();
	}
}