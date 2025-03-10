﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.AST.Node;

public class AsReferenceNode : ExpressionNode
{
    public ExpressionNode Value { get; set; }

    public AsReferenceNode(ExpressionNode value)
    {
        Value = value;
    }
}
