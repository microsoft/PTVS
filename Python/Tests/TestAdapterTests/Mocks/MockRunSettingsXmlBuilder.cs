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

namespace TestAdapterTests.Mocks
{
	class MockRunSettingsXmlBuilder
	{
		// {0} is the test results directory
		// {1} is one or more formatted _runSettingProject lines
		// {2} is 'true' or 'false' depending on whether the tests should be run
		// {3} is 'true' or 'false' depending on whether the console should be shown
		// {4} is 'True' or 'False' depending on whether code coverage is enabled
		private const string _runSettings = @"<?xml version=""1.0""?><RunSettings><DataCollectionRunSettings><DataCollectors /></DataCollectionRunSettings><RunConfiguration><ResultsDirectory>{0}</ResultsDirectory><TargetPlatform>X86</TargetPlatform><TargetFrameworkVersion>Framework45</TargetFrameworkVersion></RunConfiguration><Python><TestCases>
{1}
</TestCases>
<DryRun value=""{2}"" /><EnableCoverage>{4}</EnableCoverage><ShowConsole value=""{3}"" /></Python></RunSettings>";

		// {0} is the project name
		// {1} is the project folder
		// {2} is the interpreter path
		// {3} is the test framework
		// {4} is one or more formatted _runSettingTest lines
		// {5} is one or more formatted _runSettingEnvironment lines
		// {6} is one or more formatted _runSettingSearch lines
		// {7} is the discovery wait time
		// {8} is the workspace true/false
		// {9} is the unit test configuration formatted _unitTestConfig

		private const string _runSettingProject = @"<Project name=""{0}"" home=""{1}"" nativeDebugging="""" djangoSettingsModule="""" workingDir=""{1}"" interpreter=""{2}"" pathEnv=""PYTHONPATH"" testFramework= ""{3}"" {9} discoveryWaitTime= ""{7}"" isWorkspace=""{8}""><Environment>{5}</Environment><SearchPaths>{6}</SearchPaths>
{4}
</Project>";

		// {0} is the full path to the file
		private const string _unitTestConfig = @" unitTestRootDir=""{0}"" unitTestPattern=""{1}""";

		// {0} is the variable name
		// {1} is the variable value
		private const string _runSettingEnvironment = @"<Variable name=""{0}"" value=""{1}"" />";

		// {0} is the search path
		private const string _runSettingSearch = @"<Search value=""{0}"" />";

		// {0} is the full path to the file
		private const string _runSettingTest = @"<Test file=""{0}"" />";

		private string _framework;
		private string _resultsDir;
		private string _testDir;
		private string _interpreterPath;
		private string _unitTestConfigAttributes;
		private int _discoveryWaitTimeInSeconds;
		private bool _isWorkspace;
		private bool _coverage;
		private StringBuilder _environmentLines = new StringBuilder();
		private StringBuilder _searchLines = new StringBuilder();
		private StringBuilder _testLines = new StringBuilder();

		public MockRunSettingsXmlBuilder(string testFramework, string interpreterPath, string resultsDir, string testDir, int discoveryWaitTimeInSeconds = -1, bool isWorkspace = true)
		{
			_environmentLines = new StringBuilder();
			_searchLines = new StringBuilder();
			_testLines = new StringBuilder();
			_framework = testFramework;
			_interpreterPath = interpreterPath;
			_resultsDir = resultsDir;
			_testDir = testDir;
			_unitTestConfigAttributes = string.Empty;
			_discoveryWaitTimeInSeconds = discoveryWaitTimeInSeconds;
			_isWorkspace = isWorkspace;
		}

		public MockRunSettingsXmlBuilder WithEnvironmentVariable(string name, string val)
		{
			_environmentLines.Append(string.Format(_runSettingEnvironment, name, val));
			return this;
		}

		public MockRunSettingsXmlBuilder WithSearchPath(string searchPath)
		{
			_searchLines.Append(string.Format(_runSettingSearch, searchPath));
			return this;
		}

		public MockRunSettingsXmlBuilder WithTestFile(string filePath)
		{
			_testLines.Append(string.Format(_runSettingTest, filePath));
			return this;
		}

		public MockRunSettingsXmlBuilder WithUnitTestConfiguration(string rootFolderPath, string pattern)
		{
			_unitTestConfigAttributes = string.Format(_unitTestConfig, rootFolderPath, pattern);
			return this;
		}

		public MockRunSettingsXmlBuilder WithTestFilesFromFolder(string folderPath)
		{
			foreach (var filePath in Directory.GetFiles(folderPath, "*.py"))
			{
				_testLines.Append(string.Format(_runSettingTest, filePath));
			}
			return this;
		}

		public MockRunSettingsXmlBuilder WithCoverage()
		{
			_coverage = true;
			return this;
		}

		public string ToXml()
		{
			var xml = string.Format(
				_runSettings,
				_resultsDir,
				string.Format(
					_runSettingProject,
					Path.GetFileName(_testDir),
					_testDir,
					_interpreterPath,
					_framework,
					_testLines.ToString(),
					_environmentLines.ToString(),
					_searchLines.ToString(),
					_discoveryWaitTimeInSeconds < 0 ? string.Empty : _discoveryWaitTimeInSeconds.ToString(),
					_isWorkspace,
					_unitTestConfigAttributes
				),
				"false",
				"false",
				_coverage
			);

			return xml;
		}
	}
}
