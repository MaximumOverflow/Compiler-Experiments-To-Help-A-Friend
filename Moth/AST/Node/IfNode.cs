﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.AST.Node;

public class IfNode : StatementNode
{
    public ExpressionNode Condition { get; set; }
    public ScopeNode Then { get; set; }
    public ScopeNode? Else { get; set; }

    public IfNode(ExpressionNode condition, ScopeNode then, ScopeNode? @else)
    {
        Condition = condition;
        Then = then;
        Else = @else;
    }
}
