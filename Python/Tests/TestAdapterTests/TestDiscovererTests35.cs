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

extern alias pt;

namespace TestAdapterTests
{
	[TestClass]
	public class TestDiscovererTests35 : TestDiscovererTests
	{
		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		protected override PythonVersion Version => PythonPaths.Python35_x64 ?? PythonPaths.Python35;

		public global::System.Object PythonPaths { get; private set; }

		protected override System.String GetImportErrorFormat()
		{
			return $"ImportError: No module named '{{0}}'";
		}
	}
}
