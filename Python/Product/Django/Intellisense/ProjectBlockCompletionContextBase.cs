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
            _module = buffer.GetModuleContext();
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
