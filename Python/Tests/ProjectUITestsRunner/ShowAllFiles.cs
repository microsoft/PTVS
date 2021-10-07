// Visual Studio Shared Project
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

namespace ProjectUITestsRunner
{
	[TestClass]
	public class ShowAllFiles
	{
		#region UI test boilerplate
		public VsTestInvoker _vs => new VsTestInvoker(
			VsTestContext.Instance,
			// Remote container (DLL) name
			"Microsoft.PythonTools.Tests.ProjectUITests",
			// Remote class name
			$"ProjectUITests.{GetType().Name}"
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

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void ShowAllFilesToggle()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesToggle));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesFilesAlwaysHidden()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesFilesAlwaysHidden));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesSymLinks()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesSymLinks));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesLinked()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesLinked));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void ShowAllFilesIncludeExclude()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesIncludeExclude));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ShowAllFilesChanges()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesChanges));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ShowAllFilesHiddenFiles()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesHiddenFiles));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesOnPerUser()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesOnPerUser));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesOnPerProject()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesOnPerProject));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesOffPerUser()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesOffPerUser));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesOffPerProject()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesOffPerProject));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesDefault()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesDefault));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ShowAllMoveNotInProject()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllMoveNotInProject));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllExcludeSelected()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllExcludeSelected));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ShowAllFilesRapidChanges()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesRapidChanges));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesRapidChanges2()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesRapidChanges2));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesCopyExcludedFolderWithItemByKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesCopyExcludedFolderWithItemByKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesCopyExcludedFolderWithItemByMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesCopyExcludedFolderWithItemByMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesMoveExcludedItemToExcludedFolderByKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesMoveExcludedItemToExcludedFolderByKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ShowAllFilesMoveExcludedItemToExcludedFolderByMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.ShowAllFiles.ShowAllFilesMoveExcludedItemToExcludedFolderByMouse));
		}
	}
}