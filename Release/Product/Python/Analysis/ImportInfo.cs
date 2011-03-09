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

using System.Collections.Generic;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Stores info about import statements. Namespace will be an empty string for simple "import wpf"s
    /// </summary>
    internal class ImportInfo {
        private readonly string _namespace;
        private readonly SourceSpan _span;
        private readonly List<string[]> _types;

        public ImportInfo(string dottedName, SourceSpan span) {
            _namespace = dottedName;
            _span = span;
            _types = new List<string[]>();
        }

        public List<string[]> Types {
            get { return _types; }
        }
    }
}
