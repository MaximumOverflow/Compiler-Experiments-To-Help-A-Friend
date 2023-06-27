using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

internal sealed class Block
{
	private readonly Block? _parent;
	private readonly BlockNode _node;
	private readonly Function _function;
	public readonly FileCompilationContext Context;
	private readonly Dictionary<ReadOnlyMemory<char>, (LLVMValueRef, Type)> _variables;

	private LLVMContextRef LlvmContext => Context.GlobalContext.LlvmContext;
	public IReadOnlyDictionary<ReadOnlyMemory<char>, (LLVMValueRef, Type)> Variables => _variables;

	public Block(BlockNode node, Function function, FileCompilationContext context)
	{
		_node = node;
		Context = context;
		_function = function;
		_variables = new(MemoryStringComparer.Instance);
	}
	
	public Block(BlockNode node, Block parent)
	{
		_node = node;
		_parent = parent;
		Context = parent.Context;
		_function = parent._function;
		_variables = new(parent._variables, MemoryStringComparer.Instance);
	}

	public (LLVMValueRef, Type)? Compile(LLVMBuilderRef builder, bool connectToParent)
		=> Compile(builder, connectToParent, out _);
	
	public (LLVMValueRef, Type)? Compile(LLVMBuilderRef builder, bool connectToParent, out LLVMBasicBlockRef llvmBlock)
	{
		var parentBlock = builder.InsertBlock;
		llvmBlock = LlvmContext.AppendBasicBlock(_function, "");
		builder.PositionAtEnd(llvmBlock);

		if (_variables.Count == 0)
		{
			for (var i = 0; i < _function.Parameters.Count; i++)
			{
				var (name, type) = _function.Parameters[i];
				var variable = builder.BuildAlloca(type, name.Span);
				builder.BuildStore(_function.LlvmValue.Params[i], variable);
				_variables[name] = (variable, type);
			}
		}

		(LLVMValueRef, Type)? blockRet = default;
		foreach (var statement in _node.StatementNodes)
		{
			switch (statement)
			{
				case BlockNode blockNode:
				{
					var block = new Block(blockNode, this);
					blockRet = block.Compile(builder, true);
					break;
				}

				case ExpressionNode expr:
				{
					var (value, type) = Expressions.CompileExpression(this, builder, expr, false);
					blockRet = (value, type);
					break;
				}
				
				case ReturnNode { Value: null }:
				{
					if (_function.ReturnType.LlvmType.Kind != LLVMTypeKind.LLVMVoidTypeKind)
						throw new InvalidOperationException($"Expected value of type '{_function.ReturnType.Name}' found 'nothing'.");

					builder.BuildRetVoid();
					blockRet = default;
					break;
				}
				
				case ReturnNode { Value: var expression }:
				{
					var (value, _) = Expressions.CompileExpression(this, builder, expression, false);
					if (_function.ReturnType.LlvmType != value.TypeOf)
						throw new InvalidOperationException($"Expected value of type '{_function.ReturnType.Name}' found '{value.TypeOf.StructName}'.");

					builder.BuildRet(value);
					blockRet = default;
					break;
				}

				case VarDeclNode { Name: var name, Value: var expr }:
				{
					var (value, type) = Expressions.CompileExpression(this, builder, expr, false);
					var variable = builder.BuildAlloca(type, name.Span);
					builder.BuildStore(value, variable);
					_variables[name] = (variable, type);
					blockRet = default;
					break;
				}

				case AssignmentNode { Left.Name: var name, Right: var expr }:
				{
					var (variable, varType) = _variables[name];
					var (value, type) = Expressions.CompileExpression(this, builder, expr, false);
					
					if(varType != type) 
						throw new InvalidCastException($"Cannot convert type '{type.Name}' to type {varType.Name}.");

					builder.BuildStore(value, variable);
					blockRet = default;
					break;
				}

				case WhileNode { Condition: var expr, Block: var blockNode }:
				{
					var current = builder.InsertBlock;
					var check = LlvmContext.AppendBasicBlock(_function, "");
					var @continue = LlvmContext.AppendBasicBlock(_function, "");
					
					// Execute
					LLVMBasicBlockRef execute;
					{
						var block = new Block(blockNode, this);
						block.Compile(builder, false, out execute);
						builder.BuildBr(check);
					}

					// Check
					{
						builder.PositionAtEnd(current);
						builder.BuildBr(check);
						builder.PositionAtEnd(check);
						var (condition, type) = Expressions.CompileExpression(this, builder, expr, false);
						if (type.LlvmType.Kind != LLVMTypeKind.LLVMIntegerTypeKind || type.LlvmType.IntWidth != 1)
							throw new InvalidCastException($"Cannot convert type '{type.Name}' to type 'maybe'.");
					
						builder.BuildCondBr(condition, execute, @continue);
					}

					builder.PositionAtEnd(@continue);
					llvmBlock = @continue;
					blockRet = default;
					break;
				}

				default:
					throw new NotImplementedException($"Could not compile statement of type '{statement.GetType()}'.");
			}
		}

		if (_parent is not null)
		{
			if (llvmBlock.FirstInstruction == default)
			{
				llvmBlock.RemoveFromParent();
				builder.PositionAtEnd(parentBlock);
			}
			else if (connectToParent)
			{
				// Connect the two blocks
				builder.PositionAtEnd(parentBlock);
				builder.BuildBr(llvmBlock);
					
				// Continue execution
				builder.PositionAtEnd(llvmBlock);
				llvmBlock = LlvmContext.AppendBasicBlock(_function, "");
				builder.BuildBr(llvmBlock);
				builder.PositionAtEnd(llvmBlock);
			}
		}
		else if (_function.ReturnType.LlvmType.Kind == LLVMTypeKind.LLVMVoidTypeKind && builder.InsertBlock.Terminator == default)
		{
			builder.BuildRetVoid();
		}

		return blockRet;
	}
}