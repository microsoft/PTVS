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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Data for the <see cref="IPythonProject2.ProjectAnalyzerChanging"/> event
    /// specifying the previous and new analyzer.
    /// </summary>
    public sealed class AnalyzerChangingEventArgs : EventArgs {
        private readonly VsProjectAnalyzer _old, _new;

        /// <summary>
        /// The previous analyzer, if any.
        /// </summary>
        public VsProjectAnalyzer Old { get { return _old; } }

        /// <summary>
        /// The new analyzer, if any.
        /// </summary>
        public VsProjectAnalyzer New { get { return _new; } }

        public AnalyzerChangingEventArgs(VsProjectAnalyzer oldAnalyzer, VsProjectAnalyzer newAnalyzer) {
            _old = oldAnalyzer;
            _new = newAnalyzer;
        }
    }
}
