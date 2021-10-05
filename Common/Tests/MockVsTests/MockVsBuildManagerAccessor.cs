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

using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.VisualStudioTools.MockVsTests
{
	class MockVsBuildManagerAccessor : IVsBuildManagerAccessor
	{
		public int BeginDesignTimeBuild()
		{
			BuildParameters buildParameters = new BuildParameters(MSBuild.ProjectCollection.GlobalProjectCollection);
			BuildManager.DefaultBuildManager.BeginBuild(buildParameters);
			return VSConstants.S_OK;
		}

		public int ClaimUIThreadForBuild()
		{
			return VSConstants.S_OK;
		}

		public int EndDesignTimeBuild()
		{
			BuildManager.DefaultBuildManager.EndBuild();
			return VSConstants.S_OK;
		}

		public int Escape(string pwszUnescapedValue, out string pbstrEscapedValue)
		{
			throw new NotImplementedException();
		}

		public int GetCurrentBatchBuildId(out uint pBatchId)
		{
			throw new NotImplementedException();
		}

		public int GetSolutionConfiguration(object punkRootProject, out string pbstrXmlFragment)
		{
			throw new NotImplementedException();
		}

		public int RegisterLogger(int submissionId, object punkLogger)
		{
			return VSConstants.S_OK;
		}

		public int ReleaseUIThreadForBuild()
		{
			return VSConstants.S_OK;
		}

		public int Unescape(string pwszEscapedValue, out string pbstrUnescapedValue)
		{
			throw new NotImplementedException();
		}

		public int UnregisterLoggers(int submissionId)
		{
			return VSConstants.S_OK;
		}
	}
}
