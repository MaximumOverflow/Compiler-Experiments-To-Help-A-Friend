﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.AST.Node;

public class InferredLocalDefNode : LocalDefNode
{
    public ExpressionNode Value { get; set; }

    public InferredLocalDefNode(string name, ExpressionNode val) : base(name, null)
    {
        Value = val;
    }
}
