﻿using LLVMSharp.Interop;
using Moth.AST.Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Moth.LLVM.Data;

public class Float : Class
{
    public Float(string name, LLVMTypeRef lLVMType, PrivacyType privacy) : base(name, lLVMType, privacy)
    {
    }
}
