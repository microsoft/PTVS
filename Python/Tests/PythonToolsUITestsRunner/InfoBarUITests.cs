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
	public class InfoBarUITests
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
		public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
		[TestCleanup]
		public void TestCleanup() => VsTestContext.Instance.TestCleanup();
		[ClassCleanup]
		public static void ClassCleanup() => VsTestContext.Instance.Dispose();
		#endregion

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvProjectPrompt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvProjectPrompt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvProjectNoPromptGlobalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvProjectNoPromptGlobalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvProjectNoPromptLocalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvProjectNoPromptLocalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvWorkspacePrompt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvWorkspacePrompt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvWorkspaceNoPromptGlobalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvWorkspaceNoPromptGlobalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvWorkspaceNoPromptLocalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvWorkspaceNoPromptLocalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void VirtualEnvWorkspaceNoPromptNoReqsTxt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.VirtualEnvWorkspaceNoPromptNoReqsTxt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvProjectPrompt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvProjectPrompt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvProjectNoPromptGlobalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvProjectNoPromptGlobalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvProjectNoPromptLocalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvProjectNoPromptLocalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvWorkspacePrompt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvWorkspacePrompt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvWorkspaceNoPromptGlobalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvWorkspaceNoPromptGlobalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvWorkspaceNoPromptLocalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvWorkspaceNoPromptLocalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void CondaEnvWorkspaceNoPromptNoEnvYml()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.CondaEnvWorkspaceNoPromptNoEnvYml));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesProjectPrompt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesProjectPrompt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesProjectNoPromptNoMissingPackage()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesProjectNoPromptNoMissingPackage));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesWorkspacePrompt()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesWorkspacePrompt));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesWorkspaceNoPromptGlobalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesWorkspaceNoPromptGlobalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesWorkspaceNoPromptLocalSuppress()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesWorkspaceNoPromptLocalSuppress));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesWorkspaceNoPromptNoMissingPackage()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesWorkspaceNoPromptNoMissingPackage));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void InstallPackagesWorkspaceNoPromptGlobalDefaultEnv()
		{
			_vs.RunTest(nameof(PythonToolsUITests.InfoBarUITests.InstallPackagesWorkspaceNoPromptGlobalDefaultEnv));
		}
	}
}
