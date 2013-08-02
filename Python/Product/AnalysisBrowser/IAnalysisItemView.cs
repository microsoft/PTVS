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
using System.IO;

namespace Microsoft.PythonTools.Analysis.Browser {
    interface IAnalysisItemView {
        string Name { get; }
        string SortKey { get; }
        string DisplayType { get; }
        string SourceLocation { get; }
        IEnumerable<IAnalysisItemView> Children { get; }
        IEnumerable<IAnalysisItemView> SortedChildren { get; }
        IEnumerable<KeyValuePair<string, object>> Properties { get; }

        void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        );
    }
}
