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

namespace Microsoft.VisualStudioTools.MockVsTests
{
	public static class MockVsTestExtensions
	{
		public static IVisualStudioInstance ToMockVs(this SolutionFile self)
		{
			MockVs vs = new MockVs();
			vs.Invoke(() =>
			{
				// HACK: The default targets files require a function that we don't provide
				// The tests are mostly still broken, but they get further now. We should probably
				// move them into UI tests, as we can't emulate the MSBuild environment well enough
				// to open projects from here.
				Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.SetGlobalProperty("NugetRestoreTargets", "false");
				ErrorHandler.ThrowOnFailure(vs.Solution.OpenSolutionFile(0, self.Filename));
			});
			return vs;
		}

	}
}
