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
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Data for the <see cref="IPythonProject2.ProjectAnalyzerChanging"/> event
    /// specifying the previous and new analyzer.
    /// </summary>
    public sealed class AnalyzerChangingEventArgs : EventArgs {
        private readonly PythonAnalyzer _old, _new;

        /// <summary>
        /// The previous analyzer, if any.
        /// </summary>
        public PythonAnalyzer Old { get { return _old; } }

        /// <summary>
        /// The new analyzer, if any.
        /// </summary>
        public PythonAnalyzer New { get { return _new; } }

        public AnalyzerChangingEventArgs(PythonAnalyzer oldAnalyzer, PythonAnalyzer newAnalyzer) {
            _old = oldAnalyzer;
            _new = newAnalyzer;
        }
    }
}
