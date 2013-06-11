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
using System.Globalization;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter {
    public static class PythonInterpreterFactoryExtensions {
        /// <summary>
        /// Executes the interpreter with the specified arguments. Any output is
        /// captured and returned via the <see cref="ProcessOutput"/> object.
        /// </summary>
        internal static ProcessOutput Run(
            this IPythonInterpreterFactory factory,
            params string[] arguments) {
            return ProcessOutput.RunHiddenAndCapture(factory.Configuration.InterpreterPath, arguments);
        }

        /// <summary>
        /// Determines whether two interpreter factories are equivalent.
        /// </summary>
        public static bool IsEqual(this IPythonInterpreterFactory x, IPythonInterpreterFactory y) {
            if (x == null || y == null) {
                return x == null && y == null;
            }
            if (x.GetType() != y.GetType()) {
                return false;
            }

            return x.Id == y.Id && 
                x.Description == y.Description &&
                x.Configuration.Equals(y.Configuration);
        }
    }
}
