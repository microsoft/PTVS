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

using EnvDTE;
using System;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockVsProfferCommands : IVsProfferCommands
    {
        public void AddCommandBar(string pszCmdBarName, vsCommandBarType dwType, object pCmdBarParent, uint dwIndex, out object ppCmdBar)
        {
            ppCmdBar = null;
        }

        public void AddCommandBarControl(string pszCmdNameCanonical, object pCmdBarParent, uint dwIndex, uint dwCmdType, out object ppCmdBarCtrl)
        {
            ppCmdBarCtrl = null;
        }

        public void AddNamedCommand(ref Guid pguidPackage, ref Guid pguidCmdGroup, string pszCmdNameCanonical, out uint pdwCmdId, string pszCmdNameLocalized, string pszBtnText, string pszCmdTooltip, string pszSatelliteDLL, uint dwBitmapResourceId, uint dwBitmapImageIndex, uint dwCmdFlagsDefault, uint cUIContexts, ref Guid rgguidUIContexts)
        {
            pdwCmdId = 0;
        }

        public object FindCommandBar(IntPtr pToolbarSet, ref Guid pguidCmdGroup, uint dwMenuId)
        {
            throw new NotImplementedException();
        }

        public void RemoveCommandBar(object pCmdBar)
        {
            throw new NotImplementedException();
        }

        public void RemoveCommandBarControl(object pCmdBarCtrl)
        {
            throw new NotImplementedException();
        }

        public void RemoveNamedCommand(string pszCmdNameCanonical)
        {
            throw new NotImplementedException();
        }

        public void RenameNamedCommand(string pszCmdNameCanonical, string pszCmdNameCanonicalNew, string pszCmdNameLocalizedNew)
        {
            throw new NotImplementedException();
        }
    }
}
