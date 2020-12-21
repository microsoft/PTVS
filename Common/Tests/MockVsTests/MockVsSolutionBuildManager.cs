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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockVsSolutionBuildManager : IVsSolutionBuildManager
    {
        public int AdviseUpdateSolutionEvents(IVsUpdateSolutionEvents pIVsUpdateSolutionEvents, out uint pdwCookie)
        {
            throw new NotImplementedException();
        }

        public int CanCancelUpdateSolutionConfiguration(out int pfCanCancel)
        {
            throw new NotImplementedException();
        }

        public int CancelUpdateSolutionConfiguration()
        {
            throw new NotImplementedException();
        }

        public int DebugLaunch(uint grfLaunch)
        {
            throw new NotImplementedException();
        }

        public int FindActiveProjectCfg(IntPtr pvReserved1, IntPtr pvReserved2, IVsHierarchy pIVsHierarchy_RequestedProject, IVsProjectCfg[] ppIVsProjectCfg_Active = null)
        {
            throw new NotImplementedException();
        }

        public int GetProjectDependencies(IVsHierarchy pHier, uint celt, IVsHierarchy[] rgpHier, uint[] pcActual = null)
        {
            throw new NotImplementedException();
        }

        public int QueryBuildManagerBusy(out int pfBuildManagerBusy)
        {
            pfBuildManagerBusy = 0;
            return VSConstants.S_OK;
        }

        public int QueryDebugLaunch(uint grfLaunch, out int pfCanLaunch)
        {
            throw new NotImplementedException();
        }

        public int StartSimpleUpdateProjectConfiguration(IVsHierarchy pIVsHierarchyToBuild, IVsHierarchy pIVsHierarchyDependent, string pszDependentConfigurationCanonicalName, uint dwFlags, uint dwDefQueryResults, int fSuppressUI)
        {
            throw new NotImplementedException();
        }

        public int StartSimpleUpdateSolutionConfiguration(uint dwFlags, uint dwDefQueryResults, int fSuppressUI)
        {
            throw new NotImplementedException();
        }

        public int UnadviseUpdateSolutionEvents(uint dwCookie)
        {
            throw new NotImplementedException();
        }

        public int UpdateSolutionConfigurationIsActive(out int pfIsActive)
        {
            throw new NotImplementedException();
        }

        public int get_CodePage(out uint puiCodePage)
        {
            throw new NotImplementedException();
        }

        public int get_IsDebug(out int pfIsDebug)
        {
            throw new NotImplementedException();
        }

        public int get_StartupProject(out IVsHierarchy ppHierarchy)
        {
            throw new NotImplementedException();
        }

        public int put_CodePage(uint uiCodePage)
        {
            throw new NotImplementedException();
        }

        public int put_IsDebug(int fIsDebug)
        {
            throw new NotImplementedException();
        }

        public int set_StartupProject(IVsHierarchy pHierarchy)
        {
            throw new NotImplementedException();
        }
    }
}
