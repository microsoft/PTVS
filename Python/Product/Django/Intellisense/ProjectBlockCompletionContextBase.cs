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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class ProjectBlockCompletionContextBase : IDjangoCompletionContext {
        private readonly DjangoAnalyzer _analyzer;
        private readonly string _filename;
        private readonly IModuleContext _module;
        private HashSet<string> _loopVars;

        public ProjectBlockCompletionContextBase(DjangoAnalyzer analyzer, ITextBuffer buffer, string filename) {
            _analyzer = analyzer;
            _module = buffer.GetModuleContext(analyzer._serviceProvider);
            _filename = filename;
        }

        protected void AddLoopVariable(string name) {
            if (_loopVars == null) {
                _loopVars = new HashSet<string>();
            }
            _loopVars.Add(name);
        }

        public Dictionary<string, HashSet<AnalysisValue>> Variables {
            get {
                var res = _analyzer.GetVariablesForTemplateFile(_filename);
                if (_loopVars != null) {
                    if (res == null) {
                        res = new Dictionary<string, HashSet<AnalysisValue>>();
                    } else {
                        res = new Dictionary<string, HashSet<AnalysisValue>>(res);
                    }

                    foreach (var loopVar in _loopVars) {
                        if (!res.ContainsKey(loopVar)) {
                            res[loopVar] = new HashSet<AnalysisValue>();
                        }
                    }
                }
                return res;
            }
        }

        public Dictionary<string, TagInfo> Filters {
            get {
                return _analyzer._filters;
            }
        }

        public IModuleContext ModuleContext {
            get {
                return _module;
            }
        }
    }
}
