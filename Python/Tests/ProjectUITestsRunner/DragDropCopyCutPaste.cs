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
	public class DragDropCopyCutPaste
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
		public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
		[TestCleanup]
		public void TestCleanup() => VsTestContext.Instance.TestCleanup();
		[ClassCleanup]
		public static void ClassCleanup() => VsTestContext.Instance.Dispose();
		#endregion

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MultiPasteKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MultiPasteKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MultiPasteMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MultiPasteMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutPastePasteItem()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutPastePasteItem));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutRenamePaste()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutRenamePaste));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutDeletePaste()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutDeletePaste));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyFileToFolderTooLongKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFileToFolderTooLongKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CopyFileToFolderTooLongMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFileToFolderTooLongMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutFileToFolderTooLongKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutFileToFolderTooLongKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CutFileToFolderTooLongMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutFileToFolderTooLongMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutRenamePasteFolder()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutRenamePasteFolder));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopiedBeforeDragPastedAfterDrop()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopiedBeforeDragPastedAfterDrop));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DragToAnotherProjectKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.DragToAnotherProjectKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DragToAnotherProjectMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.DragToAnotherProjectMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutFolderPasteOnSelf()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutFolderPasteOnSelf));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DragFolderOntoSelf()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.DragFolderOntoSelf));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DragFolderOntoChild()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.DragFolderOntoChild));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutFileReplace()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutFileReplace));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutFolderAndFile()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutFolderAndFile));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CutFilePasteSameLocation()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CutFilePasteSameLocation));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DragFolderAndFileOntoSelf()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.DragFolderAndFileOntoSelf));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyFolderFromAnotherHierarchy()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFolderFromAnotherHierarchy));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyDeletePaste()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyDeletePaste));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CrossHierarchyFileDragAndDropKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CrossHierarchyFileDragAndDropKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CrossHierarchyFileDragAndDropMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CrossHierarchyFileDragAndDropMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFolderNameKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFolderNameKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFolderNameMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFolderNameMouse));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CopyDuplicateFolderNameKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyDuplicateFolderNameKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CopyDuplicateFolderNameMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyDuplicateFolderNameMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveCrossHierarchyKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveCrossHierarchyKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveCrossHierarchyMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveCrossHierarchyMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveReverseCrossHierarchyKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveReverseCrossHierarchyKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveReverseCrossHierarchyMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveReverseCrossHierarchyMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameOverwriteKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameOverwriteKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameOverwriteMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameOverwriteMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameOverwriteAllItemsKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameOverwriteAllItemsKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameOverwriteAllItemsMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameOverwriteAllItemsMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameDontOverwriteKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameDontOverwriteKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameDontOverwriteMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameDontOverwriteMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameDontOverwrite2Keyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameDontOverwrite2Keyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameDontOverwrite2Mouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameDontOverwrite2Mouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameDontOverwriteAllItemsKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameDontOverwriteAllItemsKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameDontOverwriteAllItemsMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameDontOverwriteAllItemsMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameCancelKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameCancelKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveDuplicateFileNameCancelMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDuplicateFileNameCancelMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveDoubleCrossHierarchy()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveDoubleCrossHierarchy));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DragTwiceAndOverwrite()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.DragTwiceAndOverwrite));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyFolderMissingItem()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFolderMissingItem));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyPasteMissingFile()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyPasteMissingFile));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void MoveFolderExistingFile()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveFolderExistingFile));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MoveFolderWithContents()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveFolderWithContents));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyFolderWithContents()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFolderWithContents));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void MoveProjectToSolutionFolderKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveProjectToSolutionFolderKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void MoveProjectToSolutionFolderMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.MoveProjectToSolutionFolderMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyReadOnlyFileByKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyReadOnlyFileByKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyReadOnlyFileByMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyReadOnlyFileByMouse));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CopyFileFromFolderToLinkedFolderKeyboard()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFileFromFolderToLinkedFolderKeyboard));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CopyFileFromFolderToLinkedFolderMouse()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFileFromFolderToLinkedFolderMouse));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CopyFileToFolderCrossProject()
		{
			_vs.RunTest(nameof(ProjectUITests.DragDropCopyCutPaste.CopyFileToFolderCrossProject));
		}
	}
}
