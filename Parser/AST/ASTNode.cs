namespace Squyrm.Parser.AST;

public partial interface IAstNode {}
public interface IRootDeclarationNode : IAstNode {}

public interface IStatementNode : IAstNode
{
	public bool RequiresSemicolon => true;
}


public interface IParseableNode<TSelf> : IAstNode
{
	public static abstract bool TryParse(ref TokenStream stream, out TSelf result);
}
