﻿using LLVMSharp.Interop;
using Moth.AST.Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.LLVM;

public class Variable
{
    public LLVMValueRef LLVMVariable { get; set; }
    public LLVMTypeRef LLVMType { get; set; }
    public PrivacyType Privacy { get; set; }
    public DefinitionType Type { get; set; }
    public ClassRefNode TypeRef { get; set; }
    public bool IsConstant { get; set; }

    public Variable(LLVMValueRef lLVMVariable, LLVMTypeRef lLVMType, PrivacyType privacy,
        DefinitionType type, ClassRefNode typeRef, bool isConstant)
    {
        LLVMVariable = lLVMVariable;
        LLVMType = lLVMType;
        Privacy = privacy;
        Type = type;
        TypeRef = typeRef;
        IsConstant = isConstant;
    }
}
