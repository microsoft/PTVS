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

namespace Microsoft.PythonTools.TestAdapter {
    internal sealed class TestCaseInfo {
        private readonly string _filename;
        private readonly string _method;
        private readonly string _class;
        private readonly int _startLine, _startColumn, _endLine;

        public TestCaseInfo(string filename, string className, string methodName, int startLine, int startColumn, int endLine) {
            _filename = filename;
            _class = className;
            _method = methodName;
            _startLine = startLine;
            _startColumn = startColumn;
            _endLine = endLine;
        }

        public string Filename => _filename;
        public string MethodName => _method;
        public int StartLine => _startLine;
        public int EndLine => _endLine;
        public int StartColumn => _startColumn;
        public string ClassName => _class;

        public TestCaseKind Kind {
            get {
                // Currently we don't support other test case kinds
                return TestCaseKind.UnitTest;
            }
        }

        public Dictionary<string, object> AsDictionary() {
            return new Dictionary<string, object>() {
                { TestAnalyzer.Serialize.Filename, Filename },
                { TestAnalyzer.Serialize.ClassName, ClassName },
                { TestAnalyzer.Serialize.MethodName, MethodName },
                { TestAnalyzer.Serialize.StartLine, StartLine},
                { TestAnalyzer.Serialize.StartColumn, StartColumn},
                { TestAnalyzer.Serialize.EndLine, EndLine },
                { TestAnalyzer.Serialize.Kind, Kind.ToString() },
            };
        }

        // Compare based on Python name, so that editing the file doesn't replace the object
        private object CompareKey => new { Filename, ClassName, MethodName, StartLine };

        public override bool Equals(object obj) => CompareKey.Equals((obj as TestCaseInfo)?.CompareKey);
        public override int GetHashCode() => CompareKey.GetHashCode();
    }

    internal enum TestCaseKind {
        None,
        UnitTest
    }
}
