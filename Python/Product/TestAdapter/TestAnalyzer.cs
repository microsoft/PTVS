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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.TestAdapter {
    internal sealed class TestAnalyzer : IDisposable {
        private readonly PythonAnalyzer _analyzer;
        private readonly string _containerFilePath, _codeFileBasePath;
        private readonly Uri _executorUri;
        private readonly List<IPythonProjectEntry> _entries;

        public TestAnalyzer(
            IPythonInterpreterFactory factory,
            string containerFilePath,
            string codeFileBasePath,
            Uri executorUri
        ) {
            _analyzer = PythonAnalyzer.CreateAsync(factory).WaitAndUnwrapExceptions();
            _analyzer.Limits = AnalysisLimits.GetStandardLibraryLimits();
            _analyzer.Limits.ProcessCustomDecorators = false;

            _containerFilePath = containerFilePath;
            _codeFileBasePath = codeFileBasePath;
            _executorUri = executorUri;

            _entries = new List<IPythonProjectEntry>();
        }

        public void Dispose() {
            ((IDisposable)_analyzer).Dispose();
        }

        public void AddModule(string moduleName, string filePath, TextReader source) {
            var entry = _analyzer.AddModule(moduleName, filePath);

            using (var parser = Parser.CreateParser(source, _analyzer.LanguageVersion, new ParserOptions() { BindReferences = true })) {
                entry.UpdateTree(parser.ParseFile(), null);
            }

            _entries.Add(entry);
        }

        public IEnumerable<TestCase> GetTestCases() {
            foreach (var entry in _entries) {
                entry.Analyze(CancellationToken.None, true);
            }
            _analyzer.AnalyzeQueuedEntries(CancellationToken.None);


            foreach (var entry in _entries) {
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

                        yield return CreateTestCase(
                            classValue.DeclaringModule.FilePath,
                            classValue.Name,
                            member.Key,
                            location
                        );
                    }
                }
            }
        }

        private TestCase CreateTestCase(
            string codeFilePath,
            string className,
            string methodName,
            LocationInfo sourceLocation
        ) {
            var moduleName = CommonUtils.CreateFriendlyFilePath(_codeFileBasePath, codeFilePath);
            var fullyQualifiedName = MakeFullyQualifiedTestName(moduleName, className, methodName);
            
            // If this is a runTest test we should provide a useful display name
            var displayName = methodName == "runTest" ? className : methodName;

            return new TestCase(fullyQualifiedName, _executorUri, _containerFilePath) {
                DisplayName = displayName,
                LineNumber = sourceLocation != null ? sourceLocation.Line : 0,
                CodeFilePath = GetCodeFilePath(_codeFileBasePath, sourceLocation)
            };
        }

        internal static string MakeFullyQualifiedTestName(string modulePath, string className, string methodName) {
            return modulePath + "::" + className + "::" + methodName;
        }

        internal static void ParseFullyQualifiedTestName(
            string fullyQualifiedName,
            out string modulePath,
            out string className,
            out string methodName
        ) {
            string[] parts = fullyQualifiedName.Split(new string[] { "::" }, StringSplitOptions.None);
            Debug.Assert(parts.Length == 3);
            modulePath = parts[0];
            className = parts[1];
            methodName = parts[2];
        }

        private static string GetCodeFilePath(string basePath, LocationInfo info) {
            if (info == null || string.IsNullOrEmpty(info.FilePath)) {
                return string.Empty;
            }
            return CommonUtils.GetAbsoluteFilePath(basePath, info.FilePath);
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
    }
}
