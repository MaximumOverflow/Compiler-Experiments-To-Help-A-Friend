namespace LanguageParser.AST;

internal sealed class CallNode : IStatementNode
{
	public MethodNode Method { get; }

	public CallNode(MethodNode method)
	{
		Method = method;
	}
}