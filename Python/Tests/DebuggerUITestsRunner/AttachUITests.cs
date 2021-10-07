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

namespace DebuggerUITestsRunner
{
	[TestClass]
	public class AttachUITests
	{
		#region UI test boilerplate
		public VsTestInvoker _vs => new VsTestInvoker(
			VsTestContext.Instance,
			// Remote container (DLL) name
			"Microsoft.PythonTools.Tests.DebuggerUITests",
			// Remote class name
			$"DebuggerUITests.{GetType().Name}"
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
		public void AttachBasic()
		{
			_vs.RunTest(nameof(DebuggerUITests.AttachUITests.AttachBasic));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void AttachBreakImmediately()
		{
			_vs.RunTest(nameof(DebuggerUITests.AttachUITests.AttachBreakImmediately));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void AttachUserSetsBreakpoint()
		{
			_vs.RunTest(nameof(DebuggerUITests.AttachUITests.AttachUserSetsBreakpoint));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void AttachThreadsBreakAllAndSetExitFlag()
		{
			_vs.RunTest(nameof(DebuggerUITests.AttachUITests.AttachThreadsBreakAllAndSetExitFlag));
		}

		[TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
		[TestCategory("Installed")]
		public void AttachThreadsBreakOneAndSetExitFlag()
		{
			_vs.RunTest(nameof(DebuggerUITests.AttachUITests.AttachThreadsBreakOneAndSetExitFlag));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void AttachLotsOfThreads()
		{
			_vs.RunTest(nameof(DebuggerUITests.AttachUITests.AttachLotsOfThreads));
		}
	}
}
