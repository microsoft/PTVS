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
using Microsoft.PythonTools.Debugger.DebugEngine;

namespace Microsoft.PythonTools.Debugger {
    enum FrameKind {
        None,
        Python,
        Django
    }

    internal static class FrameKindExtensions {
        internal static void GetLanguageInfo(this FrameKind self, ref string pbstrLanguage, ref Guid pguidLanguage) {
            switch (self) {
                case FrameKind.Django:
                    pbstrLanguage = "Django Templates";
                    pguidLanguage = Guid.Empty;
                    break;
                case FrameKind.Python:
                    pbstrLanguage = "Python";
                    pguidLanguage = DebuggerConstants.guidLanguagePython;
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

    }
}
