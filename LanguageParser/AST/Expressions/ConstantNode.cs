using LanguageParser.Tokenizer;
using System.CodeDom.Compiler;
using LanguageParser.Parser;
using System.Numerics;

namespace LanguageParser.AST;

internal sealed class ConstantNode : ExpressionNode, IParseableNode<ConstantNode>
{
	public object Value { get; }

	public ConstantNode(object value)
	{
		Value = value;
	}

	public override void WriteDebugString(IndentedTextWriter writer, bool indent = false)
	{
		writer.Write(nameof(ConstantNode));
		writer.Write(" { ");
		writer.Write(nameof(Value));
		writer.Write(" = ");
		writer.Write('(');
		writer.Write(Value.GetType().Name);
		writer.Write(") ");
		writer.Write(Value);
		writer.Write(" }");
	}

	public static bool TryParse(ref TokenStream stream, out ConstantNode result)
	{
		result = default!;
		var tokens = stream;

		switch (tokens.MoveNext())
		{
			case { Type: TokenType.Int } token:
			{
				result = true switch
				{
					true when int.TryParse(token.Text.Span, out var value) => new ConstantNode(value),
					true when long.TryParse(token.Text.Span, out var value) => new ConstantNode(value),
					_ => new ConstantNode(BigInteger.Parse(token.Text.Span)),
				};
				stream = tokens;
				return true;
			}

			case { Type: TokenType.Float } token:
			{
				result = new ConstantNode(decimal.Parse(token.Text.Span));
				stream = tokens;
				return true;
			}

			case { Type: TokenType.String } token:
			{
				result = new ConstantNode(token.Text);
				stream = tokens;
				return true;
			}
			
			default: return false;
		}
	}
}