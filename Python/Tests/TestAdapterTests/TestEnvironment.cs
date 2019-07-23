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
using System.IO;
using TestUtilities;

namespace TestAdapterTests {
    internal class TestEnvironment {
        public string InterpreterPath { get; set; }
        public string SourceFolderPath { get; set; }
        public string ResultsFolderPath { get; set; }
        public string TestFramework { get; set; }

        public static TestEnvironment Create(PythonVersion pythonVersion, string testFramework, bool installFramework = true) {
            var env = new TestEnvironment();
            env.TestFramework = testFramework;

            var baseDir = TestData.GetTempPath();
            env.SourceFolderPath = Path.Combine(baseDir, "Source");
            env.ResultsFolderPath = Path.Combine(baseDir, "Results");
            Directory.CreateDirectory(env.SourceFolderPath);
            Directory.CreateDirectory(env.ResultsFolderPath);

            switch (testFramework) {
                case "Pytest": {
                        var envDir = TestData.GetTempPath();
                        if (installFramework) {
                            pythonVersion.CreatePythonVirtualEnvWithPkgs(envDir, new[] { "pytest" });
                        } else {
                            pythonVersion.CreatePythonVirtualEnv(envDir);
                        }
                        env.InterpreterPath = Path.Combine(envDir, "scripts", "python.exe");
                    }
                    break;
                default:
                    // TODO: when bug #5454 is fixed, we should create an empty
                    // virtual env to ensure env does not have pytest installed
                    env.InterpreterPath = pythonVersion.InterpreterPath;
                    break;
            }

            return env;
        }
    }
}
