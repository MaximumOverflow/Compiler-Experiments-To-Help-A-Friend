﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.AST.Node;

public class ClassNode : ASTNode
{
    public string Name { get; set; }
    public PrivacyType Privacy { get; set; }
    public ScopeNode Scope { get; set; }

    public ClassNode(string name, PrivacyType privacy, ScopeNode scope)
    {
        Name = name;
        Privacy = privacy;
        Scope = scope;
    }
}