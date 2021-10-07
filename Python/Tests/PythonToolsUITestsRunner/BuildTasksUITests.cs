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

namespace PythonToolsUITestsRunner
{
	[TestClass]
	public class BuildTasksUITests
	{
		#region UI test boilerplate
		public VsTestInvoker _vs => new VsTestInvoker(
			VsTestContext.Instance,
			// Remote container (DLL) name
			"Microsoft.PythonTools.Tests.PythonToolsUITests",
			// Remote class name
			$"PythonToolsUITests.{GetType().Name}"
		);

		public TestContext TestContext { get; set; }

		[TestInitialize]
		public void TestInitialize()
		{
			VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
		}

		[TestCleanup]
		public void TestCleanup()
		{
			VsTestContext.Instance.TestCleanup();
		}

		[ClassCleanup]
		public static void ClassCleanup()
		{
			VsTestContext.Instance.Dispose();
		}
		#endregion

		#region Python 2.7 tests

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsAdded_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsAdded), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsWithResourceLabel_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsWithResourceLabel), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CustomCommandsReplWithResourceLabel_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsReplWithResourceLabel), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsRunInRepl_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunInRepl), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CustomCommandsRunProcessInRepl_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunProcessInRepl), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsRunProcessInOutput_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunProcessInOutput), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CustomCommandsRunProcessInConsole_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunProcessInConsole), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsErrorList_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsErrorList), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsRequiredPackages_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRequiredPackages), "2.7");
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsSearchPath_27()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsSearchPath), "2.7");
		}

		#endregion

		#region Python 3.5 tests

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsAdded_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsAdded), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsWithResourceLabel_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsWithResourceLabel), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsReplWithResourceLabel_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsReplWithResourceLabel), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsRunInRepl_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunInRepl), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CustomCommandsRunProcessInRepl_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunProcessInRepl), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsRunProcessInOutput_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunProcessInOutput), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CustomCommandsRunProcessInConsole_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRunProcessInConsole), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CustomCommandsErrorList_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsErrorList), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsRequiredPackages_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsRequiredPackages), "3.5");
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CustomCommandsSearchPath_35()
		{
			_vs.RunTest(nameof(PythonToolsUITests.BuildTasksUITests.CustomCommandsSearchPath), "3.5");
		}

		#endregion
	}
}
