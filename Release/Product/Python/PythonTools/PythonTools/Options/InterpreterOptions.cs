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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Captures all of the options for an interpreter.  We can mutate this instance and then only when the user
    /// commits the changes do we propagate these back to an interpreter.
    /// </summary>
    class InterpreterOptions {
        public string Display;
        public Guid Id;
        public string InterpreterPath;
        public string WindowsInterpreterPath;
        public string Architecture;
        public string Version;
        public string PathEnvironmentVariable;
        public bool Removed;
        public bool Added;
        public bool IsConfigurable;
        public bool SupportsCompletionDb;
        public IPythonInterpreterFactory Factory;
    }
}
