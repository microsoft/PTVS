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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockUIHierWinClipboardHelper : IVsUIHierWinClipboardHelper
    {
        private readonly Dictionary<uint, IVsUIHierWinClipboardHelperEvents> _sinks = new Dictionary<uint, IVsUIHierWinClipboardHelperEvents>();
        private uint _sinkCount;
        bool _wasCut = false;

        public int AdviseClipboardHelperEvents(IVsUIHierWinClipboardHelperEvents pSink, out uint pdwCookie)
        {
            _sinks[++_sinkCount] = pSink;
            pdwCookie = _sinkCount;
            return VSConstants.S_OK;
        }

        public int Copy(VisualStudio.OLE.Interop.IDataObject pDataObject)
        {
            _wasCut = false;
            return VSConstants.S_FALSE;
        }

        public int Cut(VisualStudio.OLE.Interop.IDataObject pDataObject)
        {
            _wasCut = true;
            return VSConstants.S_OK;
        }

        public int Paste(VisualStudio.OLE.Interop.IDataObject pDataObject, uint dwEffects)
        {
            foreach (var value in _sinks.Values)
            {
                value.OnPaste(_wasCut ? 1 : 0, dwEffects);
            }
            return VSConstants.S_OK;
        }

        public int UnadviseClipboardHelperEvents(uint dwCookie)
        {
            _sinks.Remove(dwCookie);
            return VSConstants.S_OK;
        }
    }
}
