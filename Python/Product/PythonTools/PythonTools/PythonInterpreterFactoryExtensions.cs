// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

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
