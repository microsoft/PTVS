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

namespace DjangoUITestsRunner
{
	[TestClass]
	public class DjangoEditingUITests
	{
		#region UI test boilerplate
		public VsTestInvoker _vs => new VsTestInvoker(
			VsTestContext.Instance,
			// Remote container (DLL) name
			"Microsoft.PythonTools.Tests.DjangoUITests",
			// Remote class name
			$"DjangoUITests.{GetType().Name}"
		);

		public TestContext TestContext { get; set; }

		[TestInitialize]
		public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
		[TestCleanup]
		public void TestCleanup() => VsTestContext.Instance.TestCleanup();
		[ClassCleanup]
		public static void ClassCleanup() => VsTestContext.Instance.Dispose();
		#endregion

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Classifications()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Classifications));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion1()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion1));
		}

		[Ignore] // https://github.com/Microsoft/PTVS/issues/2720
		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void Insertion2()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion2));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion3()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion3));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion4()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion4));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion5()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion5));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion6()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion6));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion7()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion7));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion8()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion8));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion9()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion9));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion10()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion10));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion11()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion11));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Insertion12()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Insertion12));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Deletion1()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Deletion1));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void Paste1()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.Paste1));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void SelectAllMixed1()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.SelectAllMixed1));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void SelectAllMixed2()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.SelectAllMixed2));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void SelectAllMixed3()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.SelectAllMixed3));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void SelectAllMixed4()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.SelectAllMixed4));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void SelectAllTag()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.SelectAllTag));
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void SelectAllText()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.SelectAllText));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void CutUndo()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.CutUndo));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions2()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions2));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions4()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions4));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions5()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions5));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions6()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions6));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions7()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions7));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions8()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions8));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions9()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions9));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions10()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions10));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions11()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions11));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletions12()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletions12));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletionsHtml()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletionsHtml));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletionsCss()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletionsCss));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void IntellisenseCompletionsJS()
		{
			_vs.RunTest(nameof(DjangoUITests.DjangoEditingUITests.IntellisenseCompletionsJS));
		}
	}
}
