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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class KnownTypesView : IAnalysisItemView {
        readonly IAnalysisItemView[] _children;
        
        public KnownTypesView(IPythonInterpreter interpreter, Version version) {
            int count = (int)BuiltinTypeIdExtensions.LastTypeId;
            _children = new IAnalysisItemView[count];
            for (int value = 1; value <= count; ++value) {
                var expectedName = SharedDatabaseState.GetBuiltinTypeName((BuiltinTypeId)value, version);
                string name = string.Format("{0} ({1})",
                    expectedName,
                    Enum.GetName(typeof(BuiltinTypeId), value)
                );

                IPythonType type;
                try {
                    type = interpreter.GetBuiltinType((BuiltinTypeId)value);
                    if (expectedName != type.Name) {
                        name = string.Format("{2} ({1}/{0})",
                            expectedName,
                            Enum.GetName(typeof(BuiltinTypeId), value),
                            type.Name
                        );
                    }
                } catch {
                    type = null;
                }

                if (type != null) {
                    _children[value - 1] = new ClassView(
                        null,
                        name,
                        type
                    );
                } else {
                    _children[value - 1] = new NullMember(name);
                }
            }
        }
        
        public string Name {
            get { return "Known Types"; }
        }

        public string SortKey {
            get { return "0"; }
        }

        public string DisplayType {
            get { return string.Empty; }
        }

        public string SourceLocation {
            get { return string.Empty; }
        }

        public IEnumerable<IAnalysisItemView> Children {
            get { return _children; }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get { return _children; }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties {
            get { yield break; }
        }

        public void ExportToTree(System.IO.TextWriter writer, string currentIndent, string indent, out IEnumerable<IAnalysisItemView> exportChildren) {
            exportChildren = null;
        }
    }
}
