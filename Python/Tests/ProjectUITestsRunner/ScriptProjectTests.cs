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
	public class ScriptProjectTests
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

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void RunWithoutStartupFile()
		{
			_vs.RunTest(nameof(ProjectUITests.ScriptProjectTests.RunWithoutStartupFile));
		}

		/// <summary>
		/// Renaming the folder containing the startup script should update the startup script
		/// https://nodejstools.codeplex.com/workitem/476
		/// </summary>
		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void RenameStartupFileFolder()
		{
			_vs.RunTest(nameof(ProjectUITests.ScriptProjectTests.RenameStartupFileFolder));
		}

		[TestMethod, Priority(UITestPriority.P0)]
		[TestCategory("Installed")]
		public void RenameStartupFile()
		{
			_vs.RunTest(nameof(ProjectUITests.ScriptProjectTests.RenameStartupFile));
		}
	}
}
