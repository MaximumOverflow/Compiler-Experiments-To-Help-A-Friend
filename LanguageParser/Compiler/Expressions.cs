using LanguageParser.AST;
using LLVMSharp.Interop;
using System.Numerics;

namespace LanguageParser.Compiler;

internal static class Expressions
{
	public static (LLVMValueRef, Type) CompileExpression(Block block, LLVMBuilderRef builder, ExpressionNode expr)
	{
		switch (expr)
		{
			case ConstantNode {Value: decimal num}:
			{
				var value = (double) num;
				var type = block.Context.FindType("f64");
				return (LLVMValueRef.CreateConstReal(type, value), type);
			}
			
			case ConstantNode {Value: int num}:
			{
				var type = block.Context.FindType("i32");
				return (LLVMValueRef.CreateConstInt(type, (ulong) num), type);
			}
			
			case ConstantNode {Value: long num}:
			{
				var type = block.Context.FindType("i64");
				return (LLVMValueRef.CreateConstInt(type, (ulong) num), type);
			}

			case ConstantNode {Value: ReadOnlyMemory<char> str}:
			{
				var value = builder.BuildGlobalStringPtr(str.Span, default);
				var type = block.Context.FindType("rope");
				return (value, type);
			}
			
			case NewNode node:
			{
				var type = block.Context.FindType(node.Type.Name);
				var obj = builder.BuildAlloca(type);
				foreach (var (name, valueNode) in node.MemberAssignments)
				{
					if (!type.Members.TryGetValue(name, out var member))
						throw new KeyNotFoundException($"Type '{type.Name}' has no members named '{member.Name}'.");
					
					var (value, valueType) = CompileExpression(block, builder, valueNode);
					if (valueType != member.Type)
						throw new InvalidCastException($"Cannot convert type '{valueType.Name}' to type {member.Type.Name}.");

					var memberPtr = builder.BuildStructGEP2(type.LlvmType, obj, member.Idx);
					builder.BuildStore(value, memberPtr);
				}

				return (builder.BuildLoad2(type.LlvmType, obj), type);
			}

			case VariableNode { Name: var name }:
			{
				var (variable, type) = block.Variables[name];
				return (builder.BuildLoad2(type, variable), type);
			}
			
			default: throw new NotImplementedException($"Could not compile expression of type '{expr.GetType()}'.");
		}
	}
}