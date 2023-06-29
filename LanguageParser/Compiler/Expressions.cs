using System.Diagnostics;
using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

internal static class Expressions
{
	public static Value CompileExpression(Block block, LLVMBuilderRef builder, IExpressionNode expr, bool asRef)
	{
		switch (expr)
		{
			case UnaryOperationNode { Operation: UnaryOperationType.Group, Expression: IExpressionNode vExpr }:
				return CompileExpression(block, builder, vExpr, asRef);
			
			case UnaryOperationNode { Operation: UnaryOperationType.AddrOf, Expression: IExpressionNode vExpr }:
				return CompileExpression(block, builder, vExpr, true);

			case UnaryOperationNode { Operation: UnaryOperationType.TypeId, Expression: TypeNode tExpr }:
			{
				var type = block.Context.FindType(tExpr);
				var id = (ulong)type.LlvmType.Handle;
				var value = LLVMValueRef.CreateConstInt(block.LlvmContext.Int64Type, id);
				type = block.Context.GlobalContext.DefaultTypes["i64".AsMemory()];
				return new(value, type);
			}

			case UnaryOperationNode { Operation: UnaryOperationType.ValueOf, Expression: IExpressionNode vExpr }:
			{
				var (value, type) = CompileExpression(block, builder, vExpr, false);
				if (type is not PointerType ptr)
					return ThrowArgumentException($"Type '{type}' cannot be dereferenced.");

				return new(builder.BuildLoad2(ptr.Base, value), ptr.Base);
			}

			case UnaryOperationNode { Operation: UnaryOperationType.Undefined, Expression: TypeNode tExpr }: unsafe
			{
				var type = block.Context.FindType(tExpr);
				LLVMValueRef value = LLVM.GetUndef(type.LlvmType);
				return new(value, type);
			}

			case ConstantNode {Value: decimal num}:
			{
				var value = (double) num;
				var type = block.Context.FindType("f64");
				return new(LLVMValueRef.CreateConstReal(type, value), type);
			}
			
			case ConstantNode {Value: int num}:
			{
				var type = block.Context.FindType("i32");
				return new(LLVMValueRef.CreateConstInt(type, (ulong) num), type);
			}
			
			case ConstantNode {Value: long num}:
			{
				var type = block.Context.FindType("i64");
				return new(LLVMValueRef.CreateConstInt(type, (ulong) num), type);
			}

			case ConstantNode {Value: ReadOnlyMemory<char> str}:
			{
				return block.Context.GlobalContext.MakeConstString(str);
			}

			case BlockNode blockNode:
			{
				var child = new Block(blockNode, block);
				var (value, type) = child.Compile(builder, true);
				if (type.LlvmType.Kind == LLVMTypeKind.LLVMVoidTypeKind)
					throw new InvalidOperationException("Block does not return a value.");

				return new(value, type);
			}

			case NewNode node when asRef:
			{
				var type = block.Context.FindType(node.Type);
				var obj = builder.BuildAlloca(type);
				foreach (var (name, valueNode) in node.MemberAssignments)
				{
					if (type is not StructType { Members: var members })
						return ThrowArgumentException($"Type '{type.Name}' is not a struct.");
						
					if (!members.TryGetValue(name, out var member))
						throw new KeyNotFoundException($"Type '{type.Name}' has no members named '{member.Name}'.");
					
					var (value, valueType) = CompileExpression(block, builder, valueNode, false);
					if (valueType != member.Type)
						throw new InvalidCastException($"Cannot convert type '{valueType.Name}' to type {member.Type.Name}.");

					var memberPtr = builder.BuildStructGEP2(type.LlvmType, obj, member.Idx);
					builder.BuildStore(value, memberPtr);
				}

				return asRef 
					? new(obj, type.MakePointer()) 
					: new(builder.BuildLoad2(type.LlvmType, obj), type);
			}
			
			case NewNode node: unsafe {
				var type = block.Context.FindType(node.Type);

				if (type is not StructType { Members: var members })
					return ThrowArgumentException($"Type '{type.Name}' is not a struct.");
				
				LLVMValueRef obj = LLVM.GetUndef(type.LlvmType);
				foreach (var (name, valueNode) in node.MemberAssignments)
				{
					if (!members.TryGetValue(name, out var member))
						throw new KeyNotFoundException($"Type '{type.Name}' has no members named '{member.Name}'.");
				
					var (value, valueType) = CompileExpression(block, builder, valueNode, false);
					if (valueType != member.Type)
						throw new InvalidCastException($"Cannot convert type '{valueType.Name}' to type {member.Type.Name}.");

					obj = builder.BuildInsertValue(obj, value, member.Idx);
				}

				return new(obj, type);
			}
			
			case VariableNode { Name: var name } when asRef:
			{
				var variable = block.Variables[name];
				var (value, type) = block.Variables[name];
				return new(value, type.MakePointer(variable.Constant));
			}

			case VariableNode { Name: var name }:
			{
				if (block.Variables.TryGetValue(name, out var variable))
				{
					var (value, type) = variable;
					return new(builder.BuildLoad2(type, value), type);
				}

				if (block.Context.TryFindFunction(name, out var function))
				{
					return new(function.LlvmValue, function.Type);
				}

				throw new KeyNotFoundException($"Variable '{name}' does not exist.");
			}

			case BinaryOperationNode { Left: var leftExpr, Right: VariableNode rightExpr, Operation: BinaryOperationType.Access }:
			{
				var (obj, objType) = CompileExpression(block, builder, leftExpr, asRef);

				switch (objType)
				{
					case StructType structType when asRef:
					{
						var variable = builder.BuildAlloca(objType);
						builder.BuildStore(obj, variable);
						var field = structType.Members[rightExpr.Name];
						var gep = builder.BuildStructGEP2(structType.LlvmType, variable, field.Idx);
						return new(gep, field.Type.MakePointer());
					}

					case StructType structType:
					{
						var field = structType.Members[rightExpr.Name];
						return new(builder.BuildExtractValue(obj, field.Idx), field.Type);
					}

					case PointerType { Base: StructType structType }:
					{
						var field = structType.Members[rightExpr.Name];
						var gep = builder.BuildStructGEP2(structType.LlvmType, obj, field.Idx);
						
						return asRef 
							? new(gep, field.Type.MakePointer()) 
							: new(builder.BuildLoad2(field.Type, gep), field.Type);
					}
					
					default:
						return ThrowArgumentException($"Type '{objType.Name}' does not support access operations.");
				}
			}

			case BinaryOperationNode { Left: var leftExpr, Right: var rightExpr, Operation: BinaryOperationType.Assign }:
			{
				var (variable, varType) = CompileExpression(block, builder, leftExpr, true);
				var (value, type) = CompileExpression(block, builder, rightExpr, false);
					
				if(varType is not PointerType ptr || type != ptr.Base) 
					throw new InvalidCastException($"Cannot assign type '{type.Name}' to type {varType.Name}.");

				builder.BuildStore(value, variable);
				return new(value, type);
			}
			
			case BinaryOperationNode
			{
				Right: var rightExpr,
				Operation: BinaryOperationType.Call,
				Left: VariableNode { Name.Span: "__get_reflection_metadata__" }, 
			}:
			{
				if (rightExpr is not TupleNode { Values: [ConstantNode { Value: ReadOnlyMemory<char> name }] })
					return ThrowArgumentException("Expected string literal.");

				if(!block.Context.GlobalContext.Metadata.TryGetValue(name, out var global))
					throw new KeyNotFoundException($"Unknown reflection metadata key '{name}'.");

				var type = ((PointerType) global.Type).Base;
				var value = builder.BuildLoad2(type, global.LlvmValue);
				return new Value(value, type);
			}

			case BinaryOperationNode { Left: var leftExpr, Right: TupleNode rightExpr, Operation: BinaryOperationType.Call }:
			{
				var (function, type) = CompileExpression(block, builder, leftExpr, false);

				FunctionType functionType;
				switch (type)
				{
					case FunctionType t: functionType = t; break;
					case PointerType { Base: FunctionType t }: functionType = t; break;
					default: return ThrowArgumentException($"Type '{type.Name}' is not a function type.");
				}

				Span<LLVMValueRef> parameters = stackalloc LLVMValueRef[rightExpr.Values.Count];
				for (var i = 0; i < rightExpr.Values.Count; i++)
				{
					var vExpr = rightExpr.Values[i];
					if (i >= functionType.ParameterTypes.Count)
					{
						if (functionType.Variadic)
						{
							var (value, _) = CompileExpression(block, builder, vExpr, false);
							parameters[i] = value;
						}
						else 
							return ThrowArgumentException($"Unexpected parameter value at position {i}.");
					}
					else
					{
						var expected = functionType.ParameterTypes[i];
						var (value, vType) = CompileExpression(block, builder, vExpr, false);

						if (!vType.IsCompatibleWith(expected))
							return ThrowArgumentException($"Expected parameter of type '{expected}' at position {i}, found '{vType}'.");

						parameters[i] = value;
					}
					
				}

				var result = builder.BuildCall2(type, function, parameters, "");
				return new(result, functionType.ReturnType);
			}

			case BinaryOperationNode { Left: var leftExpr, Right: var rightExpr, Operation: BinaryOperationType.Indexing }:
			{
				var (array, arrayType) = CompileExpression(block, builder, leftExpr, false);
				var (index, indexType) = CompileExpression(block, builder, rightExpr, false);

				if (arrayType is not PointerType ptr)
					return ThrowArgumentException($"Cannot index a value of type '{arrayType}'.");
				
				if(indexType.LlvmType.Kind != LLVMTypeKind.LLVMIntegerTypeKind)
					throw new ArgumentException($"'{indexType}' is not a valid index type.");

				if (indexType.LlvmType.IntWidth < 64)
					index = builder.BuildIntCast(index, block.LlvmContext.Int64Type);
				
				var value = builder.BuildGEP2(ptr.Base, array, new ReadOnlySpan<LLVMValueRef>(index), "");

				return asRef
					? new(value, ptr)
					: new(builder.BuildLoad2(ptr.Base, value), ptr.Base);
			}

			case BinaryOperationNode { Left: var leftExpr, Right: var rightExpr, Operation: var op }:
			{
				var (leftValue, leftType) = CompileExpression(block, builder, leftExpr, false);
				var (rightValue, rightType) = CompileExpression(block, builder, rightExpr, false);

				switch (leftType.LlvmType.Kind, rightType.LlvmType.Kind)
				{
					case (LLVMTypeKind.LLVMIntegerTypeKind, LLVMTypeKind.LLVMIntegerTypeKind):
						break;

					default:
					{
						if (leftType != rightType)
							return ThrowArgumentException($"Cannot perform '{op}' between '{leftType.Name}' and '{rightType.Name}'.");
						break;
					}
				}
				
				switch (leftType.LlvmType.Kind, op)
				{
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.Addition):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildAdd(leftValue, rightValue), leftType);

					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.Subtraction):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildSub(leftValue, rightValue), leftType);

					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.Multiplication):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildMul(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.Division):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildSDiv(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.Modulo):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildSRem(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.CmpLt):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, leftValue, rightValue), block.Context.FindType("maybe"));
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.CmpLe):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildICmp(LLVMIntPredicate.LLVMIntSLE, leftValue, rightValue), block.Context.FindType("maybe"));
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.CmpGt):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, leftValue, rightValue), block.Context.FindType("maybe"));
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.CmpGe):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, leftValue, rightValue), block.Context.FindType("maybe"));
					
					case (LLVMTypeKind.LLVMIntegerTypeKind, BinaryOperationType.CmpEq):
						(leftValue, rightValue) = UniformizeIntegers(leftValue, rightValue, builder);
						return new(builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, leftValue, rightValue), block.Context.FindType("maybe"));

					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, BinaryOperationType.Addition):
						return new(builder.BuildFAdd(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, BinaryOperationType.Subtraction):
						return new(builder.BuildFSub(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, BinaryOperationType.Multiplication):
						return new(builder.BuildFMul(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, BinaryOperationType.Division):
						return new(builder.BuildFDiv(leftValue, rightValue), leftType);
					
					case (LLVMTypeKind.LLVMDoubleTypeKind or LLVMTypeKind.LLVMFloatTypeKind, BinaryOperationType.Modulo):
						return new(builder.BuildFRem(leftValue, rightValue), leftType);
					
					default:
						Debugger.Break();
						throw new NotImplementedException($"Unimplemented operation '{op}' for '{leftType.Name}'.");
				}
			}
			
			default:
				Debugger.Break();
				throw new NotImplementedException($"Could not compile expression of type '{expr.GetType()}'.");
		}
	}

	private static (LLVMValueRef, LLVMValueRef) UniformizeIntegers(LLVMValueRef a, LLVMValueRef b, LLVMBuilderRef builder)
	{
		return a.TypeOf.IntWidth.CompareTo(b.TypeOf.IntWidth) switch
		{
			0 => (a, b),
			+1 when a.IsConstant && b.IsConstant => (a, LLVMValueRef.CreateConstIntCast(b, a.TypeOf, true)),
			-1 when a.IsConstant && b.IsConstant => (LLVMValueRef.CreateConstIntCast(a, b.TypeOf, true), b),
			+1 => (a, builder.BuildIntCast(b, a.TypeOf)),
			-1 => (builder.BuildIntCast(a, b.TypeOf), b),
			_ => default,
		};
	}

	private static Value ThrowArgumentException(string message)
		=> throw new ArgumentException(message);
}

internal readonly struct Value
{
	public Type Type { get; }
	public LLVMValueRef LlvmValue { get; }

	public Value(LLVMValueRef llvmValue, Type type)
	{
		Type = type;
		LlvmValue = llvmValue;
	}

	public void Deconstruct(out LLVMValueRef value, out Type type)
		=> (value, type) = (LlvmValue, Type);
}