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
	public class NewDragDropCopyCutPaste
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
		public void MoveToMissingFolderKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveToMissingFolderKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveToMissingFolderMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveToMissingFolderMouse));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveExcludedFolderKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveExcludedFolderKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveExcludedFolderMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveExcludedFolderMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveExcludedItemToFolderKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveExcludedItemToFolderKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void MoveExcludedItemToFolderMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveExcludedItemToFolderMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameSkipMoveKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNameSkipMoveKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameSkipMoveMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNameSkipMoveMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNamesSkipOneKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNamesSkipOneKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNamesSkipOneMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNamesSkipOneMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNamesFoldersSkipOneKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNamesFoldersSkipOneKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNamesFoldersSkipOneMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNamesFoldersSkipOneMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNamesCrossProjectSkipOneKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNamesCrossProjectSkipOneKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNamesCrossProjectSkipOneMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNamesCrossProjectSkipOneMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameCrossProjectSkipMoveKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNameCrossProjectSkipMoveKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameCrossProjectSkipMoveMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNameCrossProjectSkipMoveMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameCrossProjectCSharpSkipMoveKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNameCrossProjectCSharpSkipMoveKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameCrossProjectCSharpSkipMoveMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveDuplicateFileNameCrossProjectCSharpSkipMoveMouse));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveFileFromFolderToLinkedFolderKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveFileFromFolderToLinkedFolderKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveFileFromFolderToLinkedFolderMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.NewDragDropCopyCutPaste.MoveFileFromFolderToLinkedFolderMouse));
		}
	}
}
