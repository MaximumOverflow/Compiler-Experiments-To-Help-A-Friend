using LanguageParser.AST;
using LLVMSharp.Interop;

namespace LanguageParser.Compiler;

internal sealed class Block
{
	private readonly BlockNode _node;
	private readonly Function _function;
	public readonly FileCompilationContext Context;
	private readonly Dictionary<ReadOnlyMemory<char>, (LLVMValueRef, Type)> _variables;
	
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
		Context = parent.Context;
		_function = parent._function;
		_variables = new(parent._variables, MemoryStringComparer.Instance);
	}

	public void Compile(LLVMBuilderRef builder)
	{
		builder.PositionAtEnd(Context.GlobalContext.LlvmContext.AppendBasicBlock(_function.LlvmValue, ""));

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

		foreach (var statement in _node.StatementNodes)
		{
			switch (statement)
			{
				case ReturnNode { Value: null }:
				{
					if (_function.ReturnType.LlvmType.Kind != LLVMTypeKind.LLVMVoidTypeKind)
						throw new InvalidOperationException($"Expected value of type '{_function.ReturnType.Name}' found 'nothing'.");

					builder.BuildRetVoid();
					break;
				}
				
				case ReturnNode { Value: var expression }:
				{
					var (value, _) = Expressions.CompileExpression(this, builder, expression);
					if (_function.ReturnType.LlvmType != value.TypeOf)
						throw new InvalidOperationException($"Expected value of type '{_function.ReturnType.Name}' found '{value.TypeOf.StructName}'.");

					builder.BuildRet(value);
					break;
				}

				case VarDeclNode { Name: var name, Value: var expr }:
				{
					var (value, type) = Expressions.CompileExpression(this, builder, expr);
					var variable = builder.BuildAlloca(type, name.Span);
					builder.BuildStore(value, variable);
					_variables[name] = (variable, type);
					break;
				}

				case AssignmentNode { Left.Name: var name, Right: var expr }:
				{
					var (variable, varType) = _variables[name];
					var (value, type) = Expressions.CompileExpression(this, builder, expr);
					
					if(varType != type) 
						throw new InvalidCastException($"Cannot convert type '{type.Name}' to type {varType.Name}.");

					builder.BuildStore(value, variable);
					break;
				}

				default:
					throw new NotImplementedException($"Could not compile statement of type '{statement.GetType()}'.");
			}
		}
	}
}