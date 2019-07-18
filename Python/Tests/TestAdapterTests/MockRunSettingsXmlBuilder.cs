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

using Microsoft.PythonTools.Infrastructure;
using System;
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

        // {0} is the project name
        // {1} is the project folder
        // {2} is the interpreter path
        // {3} is the test framework
        // {4} is one or more formatted _runSettingTest lines
        // {5} is one or more formatted _runSettingEnvironment lines
        // {6} is one or more formatted _runSettingSearch lines

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

        public static string CreateDiscoveryContext(string testFramework, string interpreterPath, string resultsDir, string testDir, Tuple<string, string>[] environmentVariables = null, string[] searchPaths = null) {
            var tests = new StringBuilder();

            foreach (var filePath in Directory.GetFiles(testDir, "*.py")) {
                tests.Append(string.Format(_runSettingTest, filePath));
            }

            var variables = new StringBuilder();

            foreach (var variable in environmentVariables.MaybeEnumerate()) {
                variables.Append(string.Format(_runSettingEnvironment, variable.Item1, variable.Item2));
            }

            var search = new StringBuilder();

            foreach (var searchPath in searchPaths.MaybeEnumerate()) {
                search.Append(string.Format(_runSettingSearch, searchPath));
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
                    variables.ToString(),
                    search.ToString()
                ),
                "false",
                "false"
            ); ;

            return xml;
        }
    }
}
