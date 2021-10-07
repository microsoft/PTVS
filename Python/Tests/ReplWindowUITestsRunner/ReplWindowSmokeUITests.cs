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

namespace ReplWindowUITestsRunner
{
	[TestClass, Ignore]
	public abstract class ReplWindowSmokeUITests
	{
		#region UI test boilerplate
		public VsTestInvoker _vs => new VsTestInvoker(
			VsTestContext.Instance,
			// Remote container (DLL) name
			"Microsoft.PythonTools.Tests.ReplWindowUITests",
			// Remote class name
			$"ReplWindowUITests.{nameof(ReplWindowUITests)}"
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

		protected abstract string Interpreter { get; }

		#region Smoke tests

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ExecuteInReplSysArgv()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ExecuteInReplSysArgv), Interpreter);
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ExecuteInReplSysArgvScriptArgs()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ExecuteInReplSysArgvScriptArgs), Interpreter);
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ExecuteInReplSysPath()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ExecuteInReplSysPath), Interpreter);
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void ExecuteInReplUnicodeFilename()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ExecuteInReplUnicodeFilename), Interpreter);
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void CwdImport()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.CwdImport), Interpreter);
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void QuitAndReset()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.QuitAndReset), Interpreter);
		}

		[TestMethod, Priority(UITestPriority.P2)]
		[TestCategory("Installed")]
		public void PrintAllCharacters()
		{
			_vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.PrintAllCharacters), Interpreter);
		}

		#endregion
	}

	[TestClass]
	public class ReplWindowSmokeUITests27 : ReplWindowSmokeUITests
	{
		protected override string Interpreter => "Python27|Python27_x64";
	}

	[TestClass]
	public class ReplWindowSmokeUITestsIPy27 : ReplWindowSmokeUITests
	{
		protected override string Interpreter => "IronPython27|IronPython27_x64";
	}

	[TestClass]
	public class ReplWindowSmokeUITests35 : ReplWindowSmokeUITests
	{
		protected override string Interpreter => "Python35|Python36_x64";
	}

	[TestClass]
	public class ReplWindowSmokeUITests37 : ReplWindowSmokeUITests
	{
		protected override string Interpreter => "Python37|Python37_x64";
	}
}
