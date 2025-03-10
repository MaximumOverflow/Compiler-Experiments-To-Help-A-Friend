﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.AST.Node;

public class ParameterNode : ASTNode
{
    public string Name { get; set; }
    public TypeRefNode TypeRef { get; set; }
    public bool RequireRefType { get; set; }

    public ParameterNode(string name, TypeRefNode typeRef, bool requireRefType)
    {
        Name = name;
        TypeRef = typeRef;
        RequireRefType = requireRefType;
    }
}