// LineJoin.cs
// Script#/Libraries/Web
// This source code is subject to terms and conditions of the Apache License, Version 2.0.
//

using System;
using System.Runtime.CompilerServices;

namespace System.Html.Media.Graphics {

    [ScriptIgnoreNamespace]
    [ScriptImport]
    [ScriptConstants(UseNames = true)]
    public enum LineJoin {

        Miter = 0,

        Round = 1,

        Bevel = 2
    }
}
