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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;

namespace Microsoft.PythonTools.Analysis.Browser {
    class ModuleView : IAnalysisItemView {
        readonly IPythonInterpreter _interpreter;
        readonly IModuleContext _context;
        readonly string _idbPath;
        IPythonModule _module;

        public ModuleView(IPythonInterpreter interpreter, IModuleContext context, string name, string idbPath) {
            _interpreter = interpreter;
            _context = context;
            Name = name;
            _idbPath = idbPath;
        }

        public string Name { get; private set; }

        public string SortKey { get { return "0"; } }

        public string DisplayType {
            get { return "Module"; }
        }

        public override string ToString() {
            return Name;
        }

        public IEnumerable<IAnalysisItemView> Children {
            get {
                if (_module == null) {
                    _module = _interpreter.ImportModule(Name);
                }

                if (File.Exists(_idbPath)) {
                    yield return RawView.FromFile(_idbPath);
                }

                CPythonModule cpm;
                if ((cpm = _module as CPythonModule) != null && cpm._hiddenMembers != null) {
                    foreach (var keyValue in cpm._hiddenMembers) {
                        yield return MemberView.Make(_context, keyValue.Key, keyValue.Value);
                    }
                }

                foreach (var memberName in _module.GetMemberNames(_context)) {
                    yield return MemberView.Make(_context, _module, memberName);
                }
            }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get { return Children.OrderBy(c => c.SortKey).ThenBy(c => c.Name); }
        }

        public void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}{1}: {2}", currentIndent, DisplayType, Name);
            exportChildren = SortedChildren;
        }


        public string SourceLocation {
            get {
                var entry = _module as IProjectEntry;
                if (entry != null) {
                    return entry.FilePath;
                }
                return "No location";
            }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                yield return new KeyValuePair<string, object>("Location", SourceLocation);
            }
        }
    }
}
