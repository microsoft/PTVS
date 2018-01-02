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
using System.IO;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class KnownTypesView : IAnalysisItemView {
        readonly IAnalysisItemView[] _children;
        
        public KnownTypesView(IPythonInterpreter interpreter, Version version) {
            int count = (int)BuiltinTypeIdExtensions.LastTypeId;
            _children = new IAnalysisItemView[count];
            for (int value = 1; value <= count; ++value) {
                var expectedName = ((BuiltinTypeId)value).GetTypeName(version);
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

        public void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            exportChildren = null;
        }

        public void ExportToDiffable(
            TextWriter writer,
            string currentIndent,
            string indent,
            Stack<IAnalysisItemView> exportStack,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            exportChildren = null;
        }
    }
}
