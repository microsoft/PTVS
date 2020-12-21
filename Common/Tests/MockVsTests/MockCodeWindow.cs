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
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockCodeWindow : IVsCodeWindow, IVsDropdownBarManager, Microsoft.VisualStudio.OLE.Interop.IConnectionPointContainer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITextView _view;

        public MockCodeWindow(IServiceProvider serviceProvider, ITextView view)
        {
            _serviceProvider = serviceProvider;
            _view = view;
        }

        public int Close()
        {
            throw new NotImplementedException();
        }

        public int GetBuffer(out IVsTextLines ppBuffer)
        {
            throw new NotImplementedException();
        }

        public int GetEditorCaption(READONLYSTATUS dwReadOnly, out string pbstrEditorCaption)
        {
            throw new NotImplementedException();
        }

        public int GetLastActiveView(out IVsTextView ppView)
        {
            return GetPrimaryView(out ppView);
        }

        public int GetPrimaryView(out IVsTextView ppView)
        {
            var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
            var editorAdapters = compModel.GetService<IVsEditorAdaptersFactoryService>();
            ppView = editorAdapters.GetViewAdapter(_view);
            return VSConstants.S_OK;
        }

        public int GetSecondaryView(out IVsTextView ppView)
        {
            ppView = null;
            return VSConstants.E_FAIL;
        }

        public int GetViewClassID(out Guid pclsidView)
        {
            throw new NotImplementedException();
        }

        public int SetBaseEditorCaption(string[] pszBaseEditorCaption)
        {
            throw new NotImplementedException();
        }

        public int SetBuffer(IVsTextLines pBuffer)
        {
            throw new NotImplementedException();
        }

        public int SetViewClassID(ref Guid clsidView)
        {
            throw new NotImplementedException();
        }

        public void EnumConnectionPoints(out VisualStudio.OLE.Interop.IEnumConnectionPoints ppEnum)
        {
            throw new NotImplementedException();
        }

        public void FindConnectionPoint(ref Guid riid, out VisualStudio.OLE.Interop.IConnectionPoint ppCP)
        {
            ppCP = null;
        }

        public int AddDropdownBar(int cCombos, IVsDropdownBarClient pClient)
        {
            return VSConstants.E_FAIL;
        }

        public int GetDropdownBar(out IVsDropdownBar ppDropdownBar)
        {
            ppDropdownBar = null;
            return VSConstants.E_FAIL;
        }

        public int RemoveDropdownBar()
        {
            return VSConstants.E_FAIL;
        }
    }
}
