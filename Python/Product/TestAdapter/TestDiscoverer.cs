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
                if (factory == null) {
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
                    string fileAbsolutePath = GetItemAbsolutePath(projectHome, item);

                    try {
                        var entry = analyzer.AddModule(ModulePath.FromFullPath(fileAbsolutePath).FullName, fileAbsolutePath);

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
                    entry.Analyze(CancellationToken.None);
                }

                // Third pass, get the results of the analysis
                foreach (var entry in entries) {
                    foreach (var classDef in GetClasses(entry).Where(c => IsTestCaseClass(entry, c))) {
                        // Check the name of all functions on the class using the analyzer
                        // This will return functions defined on this class and base classes
                        var members = entry.Analysis.GetMembersByIndex(classDef.Name, 0);
                        foreach (var member in members) {
                            if (member.MemberType == PythonMemberType.Function ||
                                member.MemberType == PythonMemberType.Method) {
                                if (member.Name.StartsWith("test")) {
                                    SendTestCase(discoverySink,
                                        ((MSBuild.Project)proj).FullPath,
                                        classDef.Name,
                                        member.Name,
                                        entry.FilePath,
                                        member.Locations.FirstOrDefault());
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void SendTestCase(ITestCaseDiscoverySink discoverySink, string containerFilePath, string className, string methodName, string classFilePath, LocationInfo sourceLocation) {
            var fullyQualifiedName = MakeFullyQualifiedTestName(classFilePath, className, methodName);
            TestCase testCase = new TestCase(fullyQualifiedName, TestExecutor.ExecutorUri, containerFilePath);
            testCase.DisplayName = methodName;
            testCase.LineNumber = sourceLocation != null ? sourceLocation.Line : 0;
            testCase.CodeFilePath = sourceLocation != null ? sourceLocation.FilePath : String.Empty;
            discoverySink.SendTestCase(testCase);
        }

        internal static string MakeFullyQualifiedTestName(string classFilePath, string className, string methodName) {
            return Path.GetFileNameWithoutExtension(classFilePath) + "::" + className + "::" + methodName;
        }

        internal static void ParseFullyQualifiedTestName(string fullyQualifiedName, out string moduleName, out string className, out string methodName) {
            string[] parts = fullyQualifiedName.Split(new string[] { "::" } , StringSplitOptions.None);
            Debug.Assert(parts.Length == 3);
            moduleName = parts[0];
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

        private static string GetItemAbsolutePath(string baseFolder, MSBuild.ProjectItem item) {
            string filePath = item.EvaluatedInclude;
            string fileAbsolutePath = filePath;
            if (!Path.IsPathRooted(fileAbsolutePath)) {
                fileAbsolutePath = Path.Combine(baseFolder, fileAbsolutePath);
            }

            return fileAbsolutePath;
        }



        private static ClassDefinition[] GetClasses(IPythonProjectEntry entry) {
            var walker = new FindClassesWalker();
            if (entry != null && entry.Tree != null) {
                entry.Tree.Walk(walker);
            }
            return walker.GetClasses();
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

        private static bool IsTestCaseClass(IPythonProjectEntry entry, ClassDefinition classNode) {
            if (entry.IsAnalyzed) {
                foreach (var value in entry.Analysis.GetValuesByIndex(classNode.Name, classNode.StartIndex)) {
                    if (value.MemberType != PythonMemberType.Class) {
                        continue;
                    }

                    foreach (var mroClass in value.Mro) {
                        if (mroClass.Any(IsTestCaseClass)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        class FindClassesWalker : PythonWalker {
            readonly List<ClassDefinition> _classes = new List<ClassDefinition>();

            public ClassDefinition[] GetClasses() {
                return _classes.ToArray();
            }

            public override bool Walk(ClassDefinition node) {
                _classes.Add(node);
                return false;
            }

            public override bool Walk(FunctionDefinition node) {
                return false;
            }
        }
    }
}
