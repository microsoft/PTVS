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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudioTools;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.TestAdapter {
    [FileExtension(".pyproj")]
    [DefaultExecutorUri(TestExecutor.ExecutorUriString)]
    class TestDiscoverer : ITestDiscoverer {
        private readonly VisualStudioApp _app;
        private readonly IInterpreterOptionsService _interpreterService;

        public TestDiscoverer() {
            _app = VisualStudioApp.FromEnvironmentVariable();
            _interpreterService = InterpreterOptionsServiceProvider.GetService(_app);
        }

        internal TestDiscoverer(VisualStudioApp app, IInterpreterOptionsService interpreterService) {
            _app = app;
            _interpreterService = interpreterService;
        }

        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext, IMessageLogger logger, ITestCaseDiscoverySink discoverySink) {
            ValidateArg.NotNull(sources, "sources");
            ValidateArg.NotNull(discoverySink, "discoverySink");

            var buildEngine = new MSBuild.ProjectCollection();
            try {
                // Load all the test containers passed in (.pyproj msbuild files)
                foreach (string source in sources) {
                    buildEngine.LoadProject(source);
                }

                foreach (var proj in buildEngine.LoadedProjects) {
                    using (var provider = new MSBuildProjectInterpreterFactoryProvider(_interpreterService, proj)) {
                        try {
                            provider.DiscoverInterpreters();
                        } catch (InvalidDataException) {
                            // This exception can be safely ignored here.
                        }
                        var factory = provider.ActiveInterpreter;
                        if (factory == _interpreterService.NoInterpretersValue) {
                            if (logger != null) {
                                logger.SendMessage(TestMessageLevel.Warning, "No interpreters available for project " + proj.FullPath);
                            }
                            continue;
                        }

                        var projectHome = Path.GetFullPath(Path.Combine(proj.DirectoryPath, proj.GetPropertyValue(PythonConstants.ProjectHomeSetting) ?? "."));

                        // Do the analysis even if the database is not up to date. At
                        // worst, we'll get no results.
                        using (var analyzer = new TestAnalyzer(
                            factory,
                            proj.FullPath,
                            projectHome,
                            TestExecutor.ExecutorUri
                        )) {
                            // Provide all files to the test analyzer
                            foreach (var item in proj.GetItems("Compile")) {
                                string fileAbsolutePath = CommonUtils.GetAbsoluteFilePath(projectHome, item.EvaluatedInclude);
                                string fullName;

                                try {
                                    fullName = ModulePath.FromFullPath(fileAbsolutePath).ModuleName;
                                } catch (ArgumentException) {
                                    if (logger != null) {
                                        logger.SendMessage(TestMessageLevel.Warning, "File has an invalid module name: " + fileAbsolutePath);
                                    }
                                    continue;
                                }

                                try {
                                    using (var reader = new StreamReader(fileAbsolutePath)) {
                                        analyzer.AddModule(fullName, fileAbsolutePath, reader);
                                    }
                                } catch (FileNotFoundException) {
                                    // user deleted file, we send the test update, but the project
                                    // isn't saved.
#if DEBUG
                                } catch (Exception ex) {
                                    if (logger != null) {
                                        logger.SendMessage(TestMessageLevel.Warning, "Failed to discover tests in " + fileAbsolutePath);
                                        logger.SendMessage(TestMessageLevel.Informational, ex.ToString());
                                    }
                                }
#else
                                } catch (Exception) {
                                    if (logger != null) {
                                        logger.SendMessage(TestMessageLevel.Warning, "Failed to discover tests in " + fileAbsolutePath);
                                    }
                                }
#endif
                            }

                            // Send each discovered test case
                            foreach (var testCase in analyzer.GetTestCases()) {
                                discoverySink.SendTestCase(testCase);
                            }
                        }
                    }
                }
            } finally {
                // Disposing buildEngine does not clear the document cache in
                // VS 2013, so manually unload all projects before disposing.
                buildEngine.UnloadAllProjects();
                buildEngine.Dispose();
            }
        }

        internal static MSBuild.Project LoadProject(MSBuild.ProjectCollection buildEngine, string fullProjectPath) {
            var buildProject = buildEngine.GetLoadedProjects(fullProjectPath).FirstOrDefault();

            if (buildProject != null) {
                buildEngine.UnloadProject(buildProject);
            }
            return buildEngine.LoadProject(fullProjectPath);
        }
    }
}
