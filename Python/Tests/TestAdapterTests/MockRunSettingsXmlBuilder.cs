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

using System.IO;
using System.Text;

namespace TestAdapterTests {
    class MockRunSettingsXmlBuilder {
        // {0} is the test results directory
        // {1} is one or more formatted _runSettingProject lines
        // {2} is 'true' or 'false' depending on whether the tests should be run
        // {3} is 'true' or 'false' depending on whether the console should be shown
        private const string _runSettings = @"<?xml version=""1.0""?><RunSettings><DataCollectionRunSettings><DataCollectors /></DataCollectionRunSettings><RunConfiguration><ResultsDirectory>{0}</ResultsDirectory><TargetPlatform>X86</TargetPlatform><TargetFrameworkVersion>Framework45</TargetFrameworkVersion></RunConfiguration><Python><TestCases>
{1}
</TestCases>
<DryRun value=""{2}"" /><ShowConsole value=""{3}"" /></Python></RunSettings>";

        //        // {0} is the project home directory, ending with a backslash
        //        // {1} is the project filename, including extension
        //        // {2} is the interpreter path
        //        // {3} is one or more formatted _runSettingTest lines
        //        // {4} is one or more formatten _runSettingEnvironment lines
        //        private const string _runSettingProject = @"<Project path=""{0}{1}"" home=""{0}"" nativeDebugging="""" djangoSettingsModule="""" workingDir=""{0}"" interpreter=""{2}"" pathEnv=""PYTHONPATH""><Environment>{4}</Environment><SearchPaths>{5}</SearchPaths>
        //{3}
        //</Project>";

        private const string _runSettingProject = @"<Project name=""{0}"" home=""{1}"" nativeDebugging="""" djangoSettingsModule="""" workingDir=""{1}"" interpreter=""{2}"" pathEnv=""PYTHONPATH"" testFramework= ""{3}""><Environment>{5}</Environment><SearchPaths>{6}</SearchPaths>
{4}
</Project>";

        // {0} is the variable name
        // {1} is the variable value
        private const string _runSettingEnvironment = @"<Variable name=""{0}"" value=""{1}"" />";

        // {0} is the search path
        private const string _runSettingSearch = @"<Search value=""{0}"" />";

        // {0} is the full path to the file
        // {1} is the class name
        // {2} is the method name
        // {3} is the line number (1-indexed)
        // {4} is the column number (1-indexed)
        //private const string _runSettingTest = @"<Test className=""{1}"" file=""{0}"" line=""{3}"" column=""{4}"" method=""{2}"" />";
        private const string _runSettingTest = @"<Test file=""{0}"" />";

        public static string CreateDiscoveryContext(string testFramework, string interpreterPath, string resultsDir, string testDir) {
            var tests = new StringBuilder();

            foreach (var filePath in Directory.GetFiles(testDir, "*.py")) {
                tests.Append(string.Format(_runSettingTest, filePath));
            }

            var xml = string.Format(
                _runSettings,
                resultsDir,
                string.Format(
                    _runSettingProject,
                    Path.GetFileName(testDir),
                    testDir,
                    interpreterPath,
                    testFramework,
                    tests.ToString(),
                    string.Empty,
                    string.Empty
                ),
                "false",
                "false"
            );

            return xml;
        }
    }
}
