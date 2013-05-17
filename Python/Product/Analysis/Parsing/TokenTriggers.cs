/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;

namespace Microsoft.PythonTools.Parsing {

    /// <summary>
    /// See also <c>Microsoft.VisualStudio.Package.TokenTriggers</c>.
    /// </summary>
    [Flags]
    public enum TokenTriggers {
        None = 0,
        MemberSelect = 1,
        MatchBraces = 2,
        ParameterStart = 16,
        ParameterNext = 32,
        ParameterEnd = 64,
        Parameter = 128,
        MethodTip = 240,
    }

}
