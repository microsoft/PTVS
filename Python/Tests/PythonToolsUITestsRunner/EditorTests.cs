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
	public class EditorTests
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

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void AutomaticBraceCompletion()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.AutomaticBraceCompletion));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void UnregisteredFileExtensionEditor()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.UnregisteredFileExtensionEditor));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void OutliningTest()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.OutliningTest));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void OutlineNestedFuncDef()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.OutlineNestedFuncDef));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void OutliningBadForStatement()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.OutliningBadForStatement));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ClassificationTest()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ClassificationTest));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ClassificationMultiLineStringTest()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ClassificationMultiLineStringTest));
		}

		/// <summary>
		/// http://pytools.codeplex.com/workitem/749
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void ClassificationMultiLineStringTest2()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ClassificationMultiLineStringTest2));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void SignaturesTest()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.SignaturesTest));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void MultiLineSignaturesTest()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.MultiLineSignaturesTest));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CompletionsCaseSensitive()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.CompletionsCaseSensitive));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void AutoIndent()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.AutoIndent));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void AutoIndentExisting()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.AutoIndentExisting));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void TypingTest()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.TypingTest));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CompletionTests()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.CompletionTests));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void OpenInvalidUnicodeFile()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.OpenInvalidUnicodeFile));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void IndentationInconsistencyWarning()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.IndentationInconsistencyWarning));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void IndentationInconsistencyError()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.IndentationInconsistencyError));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IndentationInconsistencyIgnore()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.IndentationInconsistencyIgnore));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST), TestCategory("Squiggle")]
		[TestCategory("Installed")]
		public void ImportPresent()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportPresent));
		}

		[TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST), TestCategory("Squiggle")]
		[TestCategory("Installed")]
		public void ImportSelf()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportSelf));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST), TestCategory("Squiggle")]
		[TestCategory("Installed")]
		public void ImportMissingThenAddThenExcludeFile()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportMissingThenAddThenExcludeFile));
		}

		[TestMethod, Priority(UITestPriority.P2), TestCategory("Squiggle")]
		[TestCategory("Installed")]
		public void ImportPresentThenAddThenRemoveReference()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportPresentThenAddThenRemoveReference));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void DefaultAnalyzerForNonProjectFile()
		{
			_vs.RunTest(nameof(PythonToolsUITests.EditorTests.DefaultAnalyzerForNonProjectFile));
		}
	}
}