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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools {
    static class PythonInterpreterFactoryRunnableExtensions {
        /// <summary>
        /// Returns true if the factory can be run. This checks whether the
        /// configured InterpreterPath value is an actual file.
        /// </summary>
        internal static bool IsRunnable(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                factory.Id != InterpreterOptionsService.NoInterpretersFactoryGuid &&
                File.Exists(factory.Configuration.InterpreterPath);
        }

        /// <summary>
        /// Checks whether the factory can be run and throws the appropriate
        /// exception if it cannot.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// factory is null and parameterName is provided.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// factory is null and parameterName is not provided, or the factory
        /// has no configuration.
        /// </exception>
        /// <exception cref="NoInterpretersException">
        /// factory is the sentinel used when no environments are installed.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// factory's InterpreterPath does not exist on disk.
        /// </exception>
        internal static void ThrowIfNotRunnable(this IPythonInterpreterFactory factory, string parameterName = null) {
            if (factory == null) {
                if (string.IsNullOrEmpty(parameterName)) {
                    throw new NullReferenceException();
                } else {
                    throw new ArgumentNullException(parameterName);
                }
            } else if (factory.Configuration == null) {
                throw new NullReferenceException();
            } else if (factory.Id == InterpreterOptionsService.NoInterpretersFactoryGuid) {
                throw new NoInterpretersException();
            } else if (!File.Exists(factory.Configuration.InterpreterPath)) {
                throw new FileNotFoundException(factory.Configuration.InterpreterPath ?? "(null)");
            }
        }
    }
}
