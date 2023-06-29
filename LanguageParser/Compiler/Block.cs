using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

internal sealed class Block
{
	private readonly Block? _parent;
	private readonly BlockNode _node;
	private readonly Function _function;
	public readonly FileCompilationContext Context;
	private readonly Dictionary<ReadOnlyMemory<char>, Variable> _variables;

	public LLVMContextRef LlvmContext => Context.GlobalContext.LlvmContext;
	public IReadOnlyDictionary<ReadOnlyMemory<char>, Variable> Variables => _variables;
	
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
	
	public Value Compile(LLVMBuilderRef builder, bool connectToParent)
		=> Compile(builder, connectToParent, out _, out _);

	public Value Compile(LLVMBuilderRef builder, bool connectToParent, out bool hasReturned)
		=> Compile(builder, connectToParent, out _, out hasReturned);
	
	public Value Compile(LLVMBuilderRef builder, bool connectToParent, out LLVMBasicBlockRef llvmBlock, out bool hasReturned)
	{
		var parentBlock = builder.InsertBlock;
		llvmBlock = LlvmContext.AppendBasicBlock(_function, "");
		builder.PositionAtEnd(llvmBlock);

		if (_variables.Count == 0)
		{
			for (var i = 0; i < _function.Type.ParameterTypes.Count; i++)
			{
				var name = _function.ParameterNames[i];
				var type = _function.Type.ParameterTypes[i];
				var variable = builder.BuildAlloca(type, name.Span);
				builder.BuildStore(_function.LlvmValue.Params[i], variable);
				_variables[name] = new Variable { LlvmValue = variable, Type = type };
			}
		}

		hasReturned = false;
		Value blockRet = new(default, Context.FindType("nothing"));;
		foreach (var statement in _node.StatementNodes)
			blockRet = CompileStatement(statement, builder, ref hasReturned);

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
		else if (
			_function.Type.ReturnType.LlvmType.Kind == LLVMTypeKind.LLVMVoidTypeKind && 
			builder.InsertBlock.Terminator == default
		)
		{
			builder.BuildRetVoid();
		}

		return blockRet;
	}

	private Value CompileStatement(IStatementNode statement, LLVMBuilderRef builder, ref bool hasReturned)
	{
		if (hasReturned)
				throw new InvalidOperationException("Statements after return are not allowed.");
			
		switch (statement)
		{
			case BlockNode blockNode:
			{
				var block = new Block(blockNode, this);
				return block.Compile(builder, true, out hasReturned);
			}

			case IExpressionNode expr:
				return Expressions.CompileExpression(this, builder, expr, false);

			case ReturnNode { Value: null }:
			{
				var retT = _function.Type.ReturnType;
				if (retT.LlvmType.Kind != LLVMTypeKind.LLVMVoidTypeKind)
					throw new InvalidOperationException($"Expected value of type '{retT.Name}' found 'nothing'.");

				hasReturned = true;
				builder.BuildRetVoid();
				break;
			}
				
			case ReturnNode { Value: var expression }:
			{
				var retT = _function.Type.ReturnType;
				var (value, type) = Expressions.CompileExpression(this, builder, expression, false);
					
				if (type != retT)
					throw new InvalidOperationException($"Expected value of type '{retT.Name}' found '{type.Name}'.");

				hasReturned = true;
				builder.BuildRet(value);
				break;
			}

			case VarDeclNode { Name: var name, Value: var expr, Constant: var isConstant }:
			{
				var (value, type) = Expressions.CompileExpression(this, builder, expr, false);
				var variable = builder.BuildAlloca(type, name.Span);
				builder.BuildStore(value, variable);
				_variables[name] = new Variable
				{						
					Type = type,
					LlvmValue = variable, 
					Constant = isConstant,
				};
					
				break;
			}

			case IfNode { Condition: var expr, Then: var thenExpr, Else: var elseStatement }:
			{
				var (condition, type) = Expressions.CompileExpression(this, builder, expr, false);
				if (type != Context.FindType("maybe")) 
					throw new ArgumentException($"Expected value of type 'maybe', found {type}.");
					
				var checkBlock = builder.InsertBlock;
				var @continue = LlvmContext.AppendBasicBlock(_function, "");

				// Then
				Value thenRet;
				LLVMBasicBlockRef thenBlock;
				{
					var then = new Block(thenExpr, this);
					thenRet = then.Compile(builder, false, out thenBlock, out _);
					builder.BuildBr(@continue);
					builder.PositionAtEnd(@continue);
				}
					
				// Else
				Value elseRet;
				LLVMBasicBlockRef elseBlock;
				{
					elseBlock = LlvmContext.AppendBasicBlock(_function, "");
					builder.PositionAtEnd(elseBlock);
					if (elseStatement != null)
					{
						var dummy = hasReturned;
						elseRet = CompileStatement(elseStatement, builder, ref dummy);
					}
					
					builder.BuildBr(@continue);
					builder.PositionAtEnd(@continue);
				}

				// Check
				{
					var current = builder.InsertBlock;
					builder.PositionAtEnd(checkBlock);
					builder.BuildCondBr(condition, thenBlock, elseBlock);
					builder.PositionAtEnd(current);
				}

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
					block.Compile(builder, false, out execute, out _);
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
				break;
			}

			default:
				throw new NotImplementedException($"Could not compile statement of type '{statement.GetType()}'.");
		}
		
		return new(default, Context.FindType("nothing"));
	}
}

internal readonly struct Variable
{
	public bool Constant { get; init; }
	public required Type Type { get; init; }
	public required LLVMValueRef LlvmValue { get; init; }

	public void Deconstruct(out LLVMValueRef value, out Type type)
		=> (value, type) = (LlvmValue, Type);
}
