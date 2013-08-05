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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PythonTools.Analysis;

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

        /// <summary>
        /// Determines whether the interpreter factory contains the specified
        /// modules.
        /// </summary>
        /// <returns>The names of the modules that were found.</returns>
        internal static HashSet<string> FindModules(this IPythonInterpreterFactory factory, params string[] moduleNames) {
            var expected = new HashSet<string>(moduleNames);
            var result = new HashSet<string>();
            foreach (var mp in ModulePath.GetModulesInLib(factory)) {
                if (expected.Count == 0) {
                    break;
                }

                if (expected.Remove(mp.ModuleName)) {
                    result.Add(mp.ModuleName);
                }
            }
            return result;
        }
    }
}
