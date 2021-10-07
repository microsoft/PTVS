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
	public class SourceControl
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

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		// Currently Fails: https://pytools.codeplex.com/workitem/2609
		public void MoveFolderWithItem()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.MoveFolderWithItem));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void AddNewItem()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.AddNewItem));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void AddExistingItem()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.AddExistingItem));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void IncludeInProject()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.IncludeInProject));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void RemoveItem()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.RemoveItem));
		}

		/// <summary>
		/// Verify we get called w/ a project which does have source control enabled.
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void BasicSourceControl()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.BasicSourceControl));
		}

		/// <summary>
		/// Verify the glyph change APIs update the glyphs appropriately
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void SourceControlGlyphChanged()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.SourceControlGlyphChanged));
		}

		/// <summary>
		/// Verify we don't get called for a project which doesn't have source control enabled.
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void SourceControlNoControl()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.SourceControlNoControl));
		}

		/// <summary>
		/// Verify non-member items don't get reported as source control files
		/// 
		/// https://pytools.codeplex.com/workitem/1417
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void SourceControlExcludedFilesNotPresent()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.SourceControlExcludedFilesNotPresent));
		}

		/// <summary>
		/// Verify we get called w/ a project which does have source control enabled.
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void SourceControlRenameFolder()
		{
			_vs.RunTest(nameof(ProjectUITests.SourceControl.SourceControlRenameFolder));
		}
	}
}
