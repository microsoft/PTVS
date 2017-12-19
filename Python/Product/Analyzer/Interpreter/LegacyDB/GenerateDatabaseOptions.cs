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

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// The options that may be passed to
    /// <see cref="IPythonInterpreterFactoryWithDatabase.GenerateDatabase"/>
    /// </summary>
    [Flags]
    public enum GenerateDatabaseOptions {
        /// <summary>
        /// Runs a full analysis for the interpreter's standard library and
        /// installed packages.
        /// </summary>
        None,
        /// <summary>
        /// Skips analysis if the modification time of every file in a package
        /// is earlier than the database's time. This option prefers false
        /// negatives (that is, analyze something that did not need it) if it is
        /// likely that the results could be outdated.
        /// </summary>
        SkipUnchanged
    }
}
