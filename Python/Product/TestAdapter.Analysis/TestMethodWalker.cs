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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.TestAdapter {
    class TestMethodWalker : PythonWalker {
        private readonly PythonAst _tree;
        private readonly string _filename;
        private readonly Uri _documentUri;
        private readonly IReadOnlyList<LocationInfo> _spans;
        private bool _inClass;

        public readonly List<KeyValuePair<string, LocationInfo>> Methods = new List<KeyValuePair<string, LocationInfo>>();

        public TestMethodWalker(PythonAst tree, string filename, Uri documentUri, IEnumerable<LocationInfo> spans) {
            _tree = tree;
            _filename = filename;
            _documentUri = documentUri;
            _spans = spans.ToArray();
        }

        public override bool Walk(ClassDefinition node) {
            var start = node.GetStart(_tree);
            var end = node.GetEnd(_tree);

            foreach (var span in _spans) {
                if (span.StartLine <= end.Line &&
                    (!span.EndLine.HasValue || span.EndLine.Value >= start.Line)) {
                    _inClass = true;
                    return true;
                }
            }
            return false;
        }

        public override void PostWalk(ClassDefinition node) {
            _inClass = false;
            base.PostWalk(node);
        }

        private LocationInfo GetLoc(FunctionDefinition node) {
            var s = node.Header;
            var e = node.GetEnd(_tree);
            return new LocationInfo(
                _filename,
                _documentUri,
                s.Line, s.Column,
                e.Line, e.Column
            );
        }

        public override bool Walk(FunctionDefinition node) {
            if (_inClass) {
                try {
                    Methods.Add(new KeyValuePair<string, LocationInfo>(node.Name, GetLoc(node)));
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }
            }

            return false;
        }
    }
}
