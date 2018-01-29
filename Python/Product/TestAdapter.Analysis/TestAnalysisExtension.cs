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
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Projects;

namespace Microsoft.PythonTools.TestAdapter {
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
                    var testCases = new List<TestCaseInfo>();
                    foreach (var f in body.Split(';')) {
                        if (_analyzer.TryGetProjectEntryByPath(f, out projEntry)) {
                            testCases.AddRange(GetTestCasesFromAnalysis(projEntry));
                        } else {
                            testCases.AddRange(GetTestCasesFromAst(f));
                        }
                    }

                    return serializer.Serialize(testCases.Select(tc => tc.AsDictionary()).ToArray());
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
                if (dict.TryGetValue(Serialize.Filename, out filename) && filename != null &&
                    dict.TryGetValue(Serialize.ClassName, out className) && className != null &&
                    dict.TryGetValue(Serialize.MethodName, out methodName) && methodName != null &&
                    dict.TryGetValue(Serialize.StartLine, out startLine) && startLine != null &&
                    dict.TryGetValue(Serialize.StartColumn, out startColumn) && startColumn != null &&
                    dict.TryGetValue(Serialize.EndLine, out endLine) && endLine != null &&
                    dict.TryGetValue(Serialize.Kind, out kind) && kind != null) {
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

        public class Serialize {
            public const string Filename = "filename";
            public const string ClassName = "className";
            public const string MethodName = "methodName";
            public const string StartLine = "startLine";
            public const string StartColumn = "startColumn";
            public const string EndLine = "endLine";
            public const string Kind = "kind";
        }


        public static IEnumerable<TestCaseInfo> GetTestCasesFromAnalysis(IProjectEntry projEntry) {
            var entry = projEntry as IPythonProjectEntry;
            if (entry == null) {
                yield break;
            }
            var analysis = entry.Analysis;
            if (analysis == null || !entry.IsAnalyzed) {
                yield break;
            }

            // GetTestCaseMembers may return duplicates, so we filter in
            // this function.
            var seen = new Dictionary<string, int>();

            foreach (var classValue in GetTestCaseClasses(analysis)) {
                // Check the name of all functions on the class using the
                // analyzer. This will return functions defined on this
                // class and base classes
                foreach (var member in GetTestCaseMembers(entry.Tree, entry.FilePath, entry.DocumentUri, analysis, classValue)) {
                    var name = $"{classValue.Name}.{member.Key}";
                    // Find the definition to get the real location of the
                    // member. Otherwise decorators will confuse us.
                    var definition = entry.Analysis
                        .GetVariables(name, SourceLocation.MinValue)
                        .FirstOrDefault(v => v.Type == VariableType.Definition);

                    var location = definition?.Location ?? member.Value;

                    int endLine = location?.EndLine ?? location?.StartLine ?? 0;

                    int startLine = location?.StartLine ?? 0;
                    if (seen.TryGetValue(name, out int existingStartLine)) {
                        // Same name and same line is obviously the same
                        // test. Within one line probably means that the
                        // decorator was miscalculated, and it's best to
                        // skip it anyway. (There isn't a style guide on
                        // earth that encourages using distinct single-line
                        // tests with the same name adjacent to each other,
                        // so this should have no false positives.)
                        if (Math.Abs(startLine - existingStartLine) <= 1) {
                            continue;
                        }
                    } else {
                        seen[name] = startLine;
                    }

                    yield return new TestCaseInfo(
                        classValue.DeclaringModule?.FilePath,
                        classValue.Name,
                        member.Key,
                        location?.StartLine ?? 0,
                        location?.StartColumn ?? 1,
                        endLine
                    );
                }
            }
        }

        private static bool IsTestCaseClass(AnalysisValue cls) {
            return IsTestCaseClass(cls?.PythonType);
        }

        private static bool IsTestCaseClass(IPythonType cls) {
            if (cls == null ||
                cls.DeclaringModule == null) {
                return false;
            }
            var mod = cls.DeclaringModule.Name;
            return (mod == "unittest" || mod.StartsWithOrdinal("unittest.")) && cls.Name == "TestCase";
        }
        /// <summary>
        /// Get Test Case Members for a class.  If the class has 'test*' tests 
        /// return those.  If there aren't any 'test*' tests return (if one at 
        /// all) the runTest overridden method
        /// </summary>
        private static IEnumerable<KeyValuePair<string, LocationInfo>> GetTestCaseMembers(
            PythonAst ast,
            string sourceFile,
            Uri documentUri,
            ModuleAnalysis analysis,
            AnalysisValue classValue
        ) {

            IEnumerable<KeyValuePair<string, LocationInfo>> tests = null, runTest = null;
            if (ast != null && !string.IsNullOrEmpty(sourceFile)) {
                var walker = new TestMethodWalker(ast, sourceFile, documentUri, classValue.Locations);
                ast.Walk(walker);
                tests = walker.Methods.Where(v => v.Key.StartsWithOrdinal("test"));
                runTest = walker.Methods.Where(v => v.Key.Equals("runTest"));
            }

            var methodFunctions = classValue.GetAllMembers(analysis.InterpreterContext)
                .Where(v => v.Value.Any(m => m.MemberType == PythonMemberType.Function || m.MemberType == PythonMemberType.Method))
                .Select(v => new KeyValuePair<string, LocationInfo>(v.Key, v.Value.SelectMany(av => av.Locations).FirstOrDefault(l => l != null)));

            var analysisTests = methodFunctions.Where(v => v.Key.StartsWithOrdinal("test"));
            var analysisRunTest = methodFunctions.Where(v => v.Key.Equals("runTest"));

            tests = tests?.Concat(analysisTests) ?? analysisTests;
            runTest = runTest?.Concat(analysisRunTest) ?? analysisRunTest;

            if (tests.Any()) {
                return tests;
            } else {
                return runTest;
            }
        }

        private static IEnumerable<AnalysisValue> GetTestCaseClasses(ModuleAnalysis analysis) {
            return analysis.GetAllAvailableMembers(SourceLocation.MinValue, GetMemberOptions.ExcludeBuiltins)
                .SelectMany(m => analysis.GetValues(m.Name, SourceLocation.MinValue))
                .Where(v => v.MemberType == PythonMemberType.Class)
                .Where(v => v.Mro.SelectMany(v2 => v2).Any(IsTestCaseClass));
        }

        private static IEnumerable<IPythonType> GetTestCaseClasses(IPythonModule module, IModuleContext context) {
            foreach (var name in module.GetMemberNames(context)) {
                var cls = module.GetMember(context, name) as IPythonType;
                if (cls != null) {
                    foreach (var baseCls in cls.Mro.MaybeEnumerate()) {
                        if (baseCls.Name == "TestCase" ||
                            baseCls.Name.StartsWith("unittest.") && baseCls.Name.EndsWith(".TestCase")) {
                            yield return cls;
                        }
                    }
                }
            }
        }

        private static IEnumerable<IPythonFunction> GetTestCaseMembers(IPythonType cls, IModuleContext context) {
            var methodFunctions = cls.GetMemberNames(context).Select(n => cls.GetMember(context, n))
                .OfType<IPythonFunction>()
                .ToArray();

            var tests = methodFunctions.Where(v => v.Name.StartsWithOrdinal("test"));
            var runTest = methodFunctions.Where(v => v.Name.Equals("runTest"));

            if (tests.Any()) {
                return tests;
            } else {
                return runTest;
            }
        }

        public IEnumerable<TestCaseInfo> GetTestCasesFromAst(string path) {
            IPythonModule module;
            try {
                module = AstPythonModule.FromFile(_analyzer.Interpreter, path, _analyzer.LanguageVersion);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                return Enumerable.Empty<TestCaseInfo>();
            }

            var ctxt = _analyzer.Interpreter.CreateModuleContext();
            return GetTestCasesFromAst(module, ctxt);
        }

        internal static IEnumerable<TestCaseInfo> GetTestCasesFromAst(IPythonModule module, IModuleContext ctxt) {
            if (module == null) {
                throw new ArgumentNullException(nameof(module));
            }

            foreach (var classValue in GetTestCaseClasses(module, ctxt)) {
                // Check the name of all functions on the class using the
                // analyzer. This will return functions defined on this
                // class and base classes
                foreach (var member in GetTestCaseMembers(classValue, ctxt)) {
                    // Find the definition to get the real location of the
                    // member. Otherwise decorators will confuse us.
                    var location = (member as ILocatedMember)?.Locations?.FirstOrDefault(loc => loc != null);

                    int endLine = location?.EndLine ?? location?.StartLine ?? 0;

                    yield return new TestCaseInfo(
                        (classValue as ILocatedMember)?.Locations.FirstOrDefault()?.FilePath,
                        classValue.Name,
                        member.Name,
                        location?.StartLine ?? 0,
                        location?.StartColumn ?? 1,
                        endLine
                    );
                }
            }
        }
    }
}
