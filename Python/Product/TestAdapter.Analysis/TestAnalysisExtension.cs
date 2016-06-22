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
using System.ComponentModel.Composition;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(IAnalysisExtension))]
    [AnalysisExtensionName(Name)]
    partial class TestAnalyzer : IAnalysisExtension {
        internal const string Name = "ptvs_unittest";
        internal const string GetTestCasesCommand = "testcases";
        
        private PythonAnalyzer _analyzer;

        public string HandleCommand(string commandId, string body) {
            var serializer = new JavaScriptSerializer();
            switch (commandId) {
                case GetTestCasesCommand:
                    IProjectEntry projEntry;
                    if (_analyzer.TryGetProjectEntryByPath(body, out projEntry)) {
                        var testCases = GetTestCases(projEntry);
                        List<object> res = new List<object>();

                        foreach (var test in testCases) {
                            var item = new Dictionary<string, object>() {
                                { Serialize.Filename, test.Filename },
                                { Serialize.ClassName, test.ClassName },
                                { Serialize.MethodName, test.MethodName },
                                { Serialize.StartLine, test.StartLine},
                                { Serialize.StartColumn, test.StartColumn},
                                { Serialize.EndLine, test.EndLine },
                                { Serialize.Kind, test.Kind.ToString() },
                            };
                            res.Add(item);
                        }

                        return serializer.Serialize(res.ToArray());
                    }

                    break;
            }

            return "";
        }

        public void Register(PythonAnalyzer analyzer) {
            _analyzer = analyzer;
        }

        public static TestCaseInfo[] GetTestCases(string data) {
            var serializer = new JavaScriptSerializer();
            List<TestCaseInfo> tests = new List<TestCaseInfo>();
            foreach (var item in serializer.Deserialize<object[]>(data)) {
                var dict = item as Dictionary<string, object>;
                if (dict == null) {
                    continue;
                }

                object filename, className, methodName, startLine, startColumn, endLine, kind;
                if (dict.TryGetValue(Serialize.Filename, out filename) &&
                    dict.TryGetValue(Serialize.ClassName, out className) &&
                    dict.TryGetValue(Serialize.MethodName, out methodName) &&
                    dict.TryGetValue(Serialize.StartLine, out startLine) &&
                    dict.TryGetValue(Serialize.StartColumn, out startColumn) &&
                    dict.TryGetValue(Serialize.EndLine, out endLine) &&
                    dict.TryGetValue(Serialize.Kind, out kind)) {
                    tests.Add(
                        new TestCaseInfo(
                            filename.ToString(),
                            className.ToString(),
                            methodName.ToString(),
                            ToInt(startLine),
                            ToInt(startColumn),
                            ToInt(endLine)
                        )
                    );
                }
            }
            return tests.ToArray();
        }

        private static int ToInt(object value) {
            if (value is int) {
                return (int)value;
            }
            return 0;
        }

        class Serialize {
            public const string Filename = "filename";
            public const string ClassName = "className";
            public const string MethodName = "methodName";
            public const string StartLine = "startLine";
            public const string StartColumn = "startColumn";
            public const string EndLine = "endLine";
            public const string Kind = "kind";
        }


        public static IEnumerable<TestCaseInfo> GetTestCases(IProjectEntry projEntry) {
            var entry = projEntry as IPythonProjectEntry;
            if (entry == null) {
                yield break;
            }

            foreach (var classValue in GetTestCaseClasses(entry)) {
                // Check the name of all functions on the class using the
                // analyzer. This will return functions defined on this
                // class and base classes
                foreach (var member in GetTestCaseMembers(entry, classValue)) {
                    // Find the definition to get the real location of the
                    // member. Otherwise decorators will confuse us.
                    var definition = entry.Analysis
                        .GetVariablesByIndex(classValue.Name + "." + member.Key, 0)
                        .FirstOrDefault(v => v.Type == VariableType.Definition);

                    var location = (definition != null) ?
                        definition.Location :
                        member.Value.SelectMany(m => m.Locations).FirstOrDefault(loc => loc != null);

                    int endLine = location?.EndLine ?? location?.StartLine ?? 0;

                    yield return new TestCaseInfo(
                        classValue.DeclaringModule.FilePath,
                        classValue.Name,
                        member.Key,
                        location.StartLine,
                        location.StartColumn,
                        endLine
                    );
                }
            }
        }

        private static bool IsTestCaseClass(AnalysisValue cls) {
            if (cls == null ||
                cls.DeclaringModule != null ||
                cls.PythonType == null ||
                cls.PythonType.DeclaringModule == null) {
                return false;
            }
            var mod = cls.PythonType.DeclaringModule.Name;
            return (mod == "unittest" || mod.StartsWith("unittest.")) && cls.Name == "TestCase";
        }

        /// <summary>
        /// Get Test Case Members for a class.  If the class has 'test*' tests 
        /// return those.  If there aren't any 'test*' tests return (if one at 
        /// all) the runTest overridden method
        /// </summary>
        private static IEnumerable<KeyValuePair<string, IAnalysisSet>> GetTestCaseMembers(
            IPythonProjectEntry entry,
            AnalysisValue classValue
        ) {
            var methodFunctions = classValue.GetAllMembers(entry.Analysis.InterpreterContext)
                .Where(v => v.Value.Any(m => m.MemberType == PythonMemberType.Function || m.MemberType == PythonMemberType.Method));

            var tests = methodFunctions.Where(v => v.Key.StartsWith("test"));
            var runTest = methodFunctions.Where(v => v.Key.Equals("runTest"));

            if (tests.Any()) {
                return tests;
            } else {
                return runTest;
            }
        }

        private static IEnumerable<AnalysisValue> GetTestCaseClasses(IPythonProjectEntry entry) {
            if (entry.IsAnalyzed) {
                return entry.Analysis.GetAllAvailableMembersByIndex(0)
                    .SelectMany(m => entry.Analysis.GetValuesByIndex(m.Name, 0))
                    .Where(v => v.MemberType == PythonMemberType.Class)
                    .Where(v => v.Mro.SelectMany(v2 => v2).Any(IsTestCaseClass));
            } else {
                return Enumerable.Empty<AnalysisValue>();
            }
        }
    }
}
