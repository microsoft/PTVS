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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class ProjectBlockCompletionContextBase : IDjangoCompletionContext {
        private readonly IDjangoProjectAnalyzer _analyzer;
        private readonly string _filename;
        private HashSet<string> _loopVars;

        public ProjectBlockCompletionContextBase(IDjangoProjectAnalyzer analyzer, string filename) {
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _filename = filename ?? throw new ArgumentNullException(nameof(filename));
        }

        protected void AddLoopVariable(string name) {
            if (_loopVars == null) {
                _loopVars = new HashSet<string>();
            }
            _loopVars.Add(name);
        }

        public string[] Variables {
            get {
                var res = _analyzer.GetVariableNames(_filename);
                if (_loopVars != null) {
                    HashSet<string> tmp = new HashSet<string>(res);

                    tmp.UnionWith(_loopVars);
                    return tmp.ToArray();
                }
                return res;
            }
        }

        public Dictionary<string, string> Filters {
            get {
                return _analyzer.GetFilters();
            }
        }

        public DjangoUrl[] Urls {
            get {
                return _analyzer.GetUrls();
            }
        }

        public Dictionary<string, PythonMemberType> GetMembers(string name) {
            return _analyzer.GetMembers(_filename, name);
        }
    }
}
