using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

internal static class Expressions
{
	public static (LLVMValueRef, Type) CompileExpression(Block block, LLVMBuilderRef builder, ExpressionNode expr, bool asRef)
	{
		switch (expr)
		{
			case GroupNode node:
				return CompileExpression(block, builder, node.Expression, asRef);
			
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

			case BlockNode blockNode:
			{
				var child = new Block(blockNode, block);
				return child.Compile(builder, true) switch
				{
					{} value => value,
					null => throw new InvalidOperationException("Block does not return a value."),
				};
			}

			case NewNode node when asRef:
			{
				var type = block.Context.FindType(node.Type.Name);
				var obj = builder.BuildAlloca(type);
				foreach (var (name, valueNode) in node.MemberAssignments)
				{
					if (!type.Members.TryGetValue(name, out var member))
						throw new KeyNotFoundException($"Type '{type.Name}' has no members named '{member.Name}'.");
					
					var (value, valueType) = CompileExpression(block, builder, valueNode, false);
					if (valueType != member.Type)
						throw new InvalidCastException($"Cannot convert type '{valueType.Name}' to type {member.Type.Name}.");

					var memberPtr = builder.BuildStructGEP2(type.LlvmType, obj, member.Idx);
					builder.BuildStore(value, memberPtr);
				}

				return asRef ? (obj, type.MakePointer()) : (builder.BuildLoad2(type.LlvmType, obj), type);
			}
			
			case NewNode node: unsafe {
				var type = block.Context.FindType(node.Type.Name);
				LLVMValueRef obj = LLVM.GetUndef(type.LlvmType);
				foreach (var (name, valueNode) in node.MemberAssignments)
				{
					if (!type.Members.TryGetValue(name, out var member))
						throw new KeyNotFoundException($"Type '{type.Name}' has no members named '{member.Name}'.");
				
					var (value, valueType) = CompileExpression(block, builder, valueNode, false);
					if (valueType != member.Type)
						throw new InvalidCastException($"Cannot convert type '{valueType.Name}' to type {member.Type.Name}.");

					obj = builder.BuildInsertValue(obj, value, member.Idx);
				}

				return (obj, type);
			}
			
			case VariableNode { Name: var name } when asRef:
			{
				var (variable, type) = block.Variables[name];
				return (variable, type.MakePointer());
			}

			case VariableNode { Name: var name }:
			{
				var (variable, type) = block.Variables[name];
				return (builder.BuildLoad2(type, variable), type);
			}

			case BinaryOperationNode { Left: var leftExpr, Right: VariableNode rightExpr, Operation: OperationType.Access }:
			{
				var (obj, objType) = CompileExpression(block, builder, leftExpr, asRef);

				if (objType.LlvmType.Kind != LLVMTypeKind.LLVMPointerTypeKind)
				{
					if (!asRef)
					{
						var valueField = objType.Members[rightExpr.Name];
						return (builder.BuildExtractValue(obj, valueField.Idx), valueField.Type);
					}
					
					var variable = builder.BuildAlloca(objType);
					builder.BuildStore(obj, variable);
					obj = variable;
					objType = objType.MakePointer();
				}
				
				var field = objType.Base!.Members[rightExpr.Name];
				var gep = builder.BuildStructGEP2(objType.Base.LlvmType, obj, field.Idx);
				return asRef ? (gep, field.Type.MakePointer()) : (builder.BuildLoad2(field.Type, obj), field.Type);
			}

			case BinaryOperationNode { Left: var leftExpr, Right: var rightExpr, Operation: var op }:
			{
				var (leftValue, leftType) = CompileExpression(block, builder, leftExpr, false);
				var (rightValue, rightType) = CompileExpression(block, builder, rightExpr, false);

				if (leftType != rightType)
					throw new ArgumentException($"Cannot perform '{op}' between '{leftType.Name}' and '{rightType.Name}'.");
				
				switch (leftType.LlvmType.Kind, op)
				{
					case (LLVMTypeKind.LLVMIntegerTypeKind, OperationType.Addition):
						return (builder.BuildAdd(leftValue, rightValue), leftType);

					case (LLVMTypeKind.LLVMIntegerTypeKind, OperationType.Subtraction):
						return (builder.BuildSub(leftValue, rightValue), leftType);

					case (LLVMTypeKind.LLVMIntegerTypeKind, OperationType.Multiplication):
						return (builder.BuildMul(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, OperationType.Division):
						return (builder.BuildSDiv(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, OperationType.Modulo):
						return (builder.BuildSRem(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, OperationType.CmpLt):
						return (builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, leftValue, rightValue), block.Context.FindType("maybe"));

					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, OperationType.Addition):
						return (builder.BuildFAdd(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, OperationType.Subtraction):
						return (builder.BuildFSub(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, OperationType.Multiplication):
						return (builder.BuildFMul(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, OperationType.Division):
						return (builder.BuildFDiv(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, OperationType.Modulo):
						return (builder.BuildFRem(leftValue, rightValue), leftType);
					
					default:
						throw new NotImplementedException($"Unimplemented operation '{op}' for '{leftType.Name}'.");
				}
			}
			
			default: throw new NotImplementedException($"Could not compile expression of type '{expr.GetType()}'.");
		}
	}
}