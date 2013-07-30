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
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudioTools;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.TestAdapter {
    [FileExtension(".pyproj")]
    [DefaultExecutorUri(TestExecutor.ExecutorUriString)]
    class TestDiscoverer : ITestDiscoverer {
        readonly IInterpreterOptionsService _interpService;

        public TestDiscoverer() {
            _interpService = InterpreterOptionsServiceProvider.GetService();
        }

        internal TestDiscoverer(IInterpreterOptionsService interpService) {
            _interpService = interpService;
        }

        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink) {
            ValidateArg.NotNull(sources, "sources");
            ValidateArg.NotNull(discoverySink, "discoverySink");

            var buildEngine = new MSBuild.ProjectCollection();

            // Load all the test containers passed in (.pyproj msbuild files)
            foreach (string source in sources) {
                buildEngine.LoadProject(source);
            }

            foreach (var proj in buildEngine.LoadedProjects) {
                var provider = new MSBuildProjectInterpreterFactoryProvider(_interpService, proj);
                try {
                    provider.DiscoverInterpreters();
                } catch (InvalidDataException) {
                    // This exception can be safely ignored here.
                }
                var factory = provider.ActiveInterpreter;
                if (factory == _interpService.NoInterpretersValue) {
                    logger.SendMessage(TestMessageLevel.Warning, "No interpreters available for project " + proj.FullPath);
                    continue;
                }

                var projectHome = Path.GetFullPath(Path.Combine(proj.DirectoryPath, proj.GetPropertyValue(PythonConstants.ProjectHomeSetting) ?? "."));

                // Do the analysis even if the database is not up to date. At
                // worst, we'll get no results.
                var analyzer = new PythonAnalyzer(factory);
                analyzer.Limits = AnalysisLimits.GetStandardLibraryLimits();
                var entries = new List<IPythonProjectEntry>();

                // First pass, prepare each file for analysis
                foreach (var item in ((MSBuild.Project)proj).GetItems("Compile")) {
                    string fileAbsolutePath = CommonUtils.GetAbsoluteFilePath(projectHome, item.EvaluatedInclude);
                    string fullName;

                    try {
                        fullName = ModulePath.FromFullPath(fileAbsolutePath).FullName;
                    } catch (ArgumentException) {
                        logger.SendMessage(TestMessageLevel.Warning, "File has an invalid module name: " + fileAbsolutePath);
                        continue;
                    }

                    try {
                        var entry = analyzer.AddModule(fullName, fileAbsolutePath);

                        using (var reader = new StreamReader(fileAbsolutePath))
                        using (var parser = Parser.CreateParser(reader, factory.GetLanguageVersion(), new ParserOptions() { BindReferences = true })) {
                            entry.UpdateTree(parser.ParseFile(), null);
                        }

                        entries.Add(entry);
#if DEBUG
                    } catch (Exception ex) {
                        logger.SendMessage(TestMessageLevel.Warning, "Failed to discover tests in " + fileAbsolutePath);
                        logger.SendMessage(TestMessageLevel.Informational, ex.ToString());
                    }
#else
                    } catch (Exception) {
                        logger.SendMessage(TestMessageLevel.Warning, "Failed to discover tests in " + fileAbsolutePath);
                    }
#endif
                }

                // Second pass, analyze
                foreach (var entry in entries) {
                    entry.Analyze(CancellationToken.None, true);
                }
                analyzer.AnalyzeQueuedEntries(CancellationToken.None);

                // Third pass, get the results of the analysis
                foreach (var entry in entries) {
                    foreach (var classValue in GetTestCaseClasses(entry)) {
                        // Check the name of all functions on the class using the analyzer
                        // This will return functions defined on this class and base classes
                        var members = classValue.GetAllMembers(entry.Analysis.InterpreterContext)
                            .Values
                            .SelectMany(v => v)
                            .Where(v => v.MemberType == PythonMemberType.Function || v.MemberType == PythonMemberType.Method)
                            .Where(v => v.Name.StartsWith("test"));
                        foreach (var member in members) {
                            SendTestCase(discoverySink,
                                ((MSBuild.Project)proj).FullPath,
                                projectHome,
                                classValue.DeclaringModule.FilePath,
                                classValue.Name,
                                member.Name,
                                member.Locations.FirstOrDefault());
                        }
                    }
                }
            }
        }

        private static void SendTestCase(ITestCaseDiscoverySink discoverySink, string containerFilePath, string codeFileBasePath, string codeFilePath, string className, string methodName, LocationInfo sourceLocation) {
            var moduleName = CommonUtils.CreateFriendlyFilePath(codeFileBasePath, codeFilePath);
            var fullyQualifiedName = MakeFullyQualifiedTestName(moduleName, className, methodName);
            TestCase testCase = new TestCase(fullyQualifiedName, TestExecutor.ExecutorUri, containerFilePath);
            testCase.DisplayName = methodName;
            testCase.LineNumber = sourceLocation != null ? sourceLocation.Line : 0;
            testCase.CodeFilePath = GetCodeFilePath(sourceLocation, codeFileBasePath);
            discoverySink.SendTestCase(testCase);
        }

        private static string GetCodeFilePath(LocationInfo info, string basePath) {
            if (info == null || string.IsNullOrEmpty(info.FilePath)) {
                return string.Empty;
            }
            return CommonUtils.GetAbsoluteFilePath(basePath, info.FilePath);
        }

        internal static string MakeFullyQualifiedTestName(string modulePath, string className, string methodName) {
            return modulePath + "::" + className + "::" + methodName;
        }

        internal static void ParseFullyQualifiedTestName(string fullyQualifiedName, out string modulePath, out string className, out string methodName) {
            string[] parts = fullyQualifiedName.Split(new string[] { "::" }, StringSplitOptions.None);
            Debug.Assert(parts.Length == 3);
            modulePath = parts[0];
            className = parts[1];
            methodName = parts[2];
        }

        internal static MSBuild.Project LoadProject(MSBuild.ProjectCollection buildEngine, string fullProjectPath) {
            var buildProject = buildEngine.GetLoadedProjects(fullProjectPath).FirstOrDefault();

            if (buildProject != null) {
                buildEngine.UnloadProject(buildProject);
            }
            return buildEngine.LoadProject(fullProjectPath);
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
    }
}
