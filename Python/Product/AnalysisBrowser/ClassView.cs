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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class ClassView : MemberView {
        readonly IPythonType _type;
        
        public ClassView(IModuleContext context, string name, IPythonType member)
            : base(context, name, member) {
            _type = member;
        }
        
        public string OriginalName {
            get { return _type.Name; }
        }

        public override string SortKey {
            get { return "1"; }
        }

        public override string DisplayType {
            get { return "Type"; }
        }

        public override IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                foreach (var p in base.Properties) {
                    yield return p;
                }

                yield return new KeyValuePair<string, object>("Original Name", OriginalName);

                int i = 1;
                foreach(var c in _type.Mro) {
                    yield return new KeyValuePair<string, object>(string.Format("MRO #{0}", i++), MemberView.Make(_context, c == null ? "(null)" : c.Name, c));
                }
            }
        }

        public override void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}class {1}", currentIndent, Name);
            exportChildren = SortedChildren;
        }
    }
}
