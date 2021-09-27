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

extern alias pt;

namespace TestAdapterTests
{
    internal class TestEnvironment
    {
        private static Dictionary<string, TestEnvironment> _environmentsMap = new Dictionary<string, TestEnvironment>();

        public string InterpreterPath { get; set; }
        public string SourceFolderPath { get; set; }
        public string ResultsFolderPath { get; set; }
        public string TestFramework { get; set; }

        public Uri ExecutionUri
        {
            get
            {
                switch (TestFramework)
                {
                    case "Pytest": return pt.Microsoft.PythonTools.PythonConstants.PytestExecutorUri;
                    case "Unittest": return pt.Microsoft.PythonTools.PythonConstants.UnitTestExecutorUri;
                    default: Assert.Fail("unexpected test framework"); return null;
                }
            }
        }

        public static TestEnvironment GetOrCreate(PythonVersion pythonVersion, string testFramework, bool installFramework = true, bool installCoverage = false)
        {
            var testEnvironmentId = $"{pythonVersion.ToString().ToLower()}:{testFramework.ToLower()}:{installFramework.ToString()}:{installCoverage.ToString()}";
            if (_environmentsMap.TryGetValue(testEnvironmentId, out TestEnvironment foundEnv))
            {
                SetDirectories(foundEnv);
                return foundEnv;
            }

            var env = new TestEnvironment();
            env.TestFramework = testFramework;

            SetDirectories(env);

            switch (testFramework)
            {
                case "Pytest":
                    {
                        var envDir = TestData.GetTempPath();
                        var packages = new List<string>();
                        if (installFramework)
                        {
                            packages.Add("pytest");
                        }
                        if (installCoverage)
                        {
                            packages.Add("coverage");
                        }
                        pythonVersion.CreateVirtualEnv(envDir, packages);
                        env.InterpreterPath = Path.Combine(envDir, "scripts", "python.exe");
                    }
                    break;
                default:
                    if (HasPackage(pythonVersion.PrefixPath, "pytest") || installCoverage)
                    {
                        // Create an empty virtual env to ensure we don't accidentally rely on pytest
                        // (which was bug https://github.com/microsoft/PTVS/issues/5454)
                        var envDir = TestData.GetTempPath();
                        var packages = new List<string>();
                        if (installCoverage)
                        {
                            packages.Add("coverage");
                        }
                        pythonVersion.CreateVirtualEnv(envDir, packages);
                        env.InterpreterPath = Path.Combine(envDir, "scripts", "python.exe");
                    }
                    else
                    {
                        env.InterpreterPath = pythonVersion.InterpreterPath;
                    }
                    break;
            }

            _environmentsMap.Add(testEnvironmentId, env);

            return env;
        }

        public static void Clear()
        {
            _environmentsMap.Clear();
        }

        private static void SetDirectories(TestEnvironment env)
        {
            var baseDir = TestData.GetTempPath();
            env.SourceFolderPath = Path.Combine(baseDir, "Source");
            env.ResultsFolderPath = Path.Combine(baseDir, "Results");
            Directory.CreateDirectory(env.SourceFolderPath);
            Directory.CreateDirectory(env.ResultsFolderPath);
        }

        private static bool HasPackage(string prefixPath, string packageName)
        {
            foreach (var p in Directory.EnumerateDirectories(Path.Combine(prefixPath, "Lib", "site-packages")))
            {
                if (Path.GetFileName(p).StartsWith(packageName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
