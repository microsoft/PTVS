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
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class ValueView : MemberView {
        readonly IPythonConstant _value;

        public ValueView(IModuleContext context, string name, IPythonConstant member)
            : base(context, name, member) {
            _value = member;
        }

        public override string SortKey {
            get { return "3"; }
        }

        public override string DisplayType {
            get { return "Constant"; }
        }

        public override IEnumerable<IAnalysisItemView> Children {
            get {
                if (_value != null && _value.Type != null) {
                    yield return MemberView.Make(_context, _value.Type.Name, _value.Type);
                }
            }
        }

        public override void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}{1}: {2} = {3}", currentIndent, DisplayType, Name, _value.Type.Name);
            exportChildren = null;
        }
    }
}
