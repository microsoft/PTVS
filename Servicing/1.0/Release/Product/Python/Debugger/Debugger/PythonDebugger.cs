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

using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Debugger {
    class PythonDebugger {
        /// <summary>
        /// Creates a new PythonProcess object for debugging.  The process does not start until Start is called 
        /// on the returned PythonProcess object.
        /// </summary>
        public PythonProcess CreateProcess(PythonLanguageVersion langVersion, string exe, string args, string dir, string env, string interpreterOptions = null) {
            return new PythonProcess(langVersion, exe, args, dir, env, interpreterOptions);
        }
    }
}
