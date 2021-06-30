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

using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    class MockOleComponentManager : IOleComponentManager
    {
        private readonly Dictionary<uint, IOleComponent> _idleComponents = new Dictionary<uint, IOleComponent>();
        private uint _idleCount;

        public int FContinueIdle()
        {
            throw new NotImplementedException();
        }

        public int FCreateSubComponentManager(object piunkOuter, object piunkServProv, ref Guid riid, out IntPtr ppvObj)
        {
            throw new NotImplementedException();
        }

        public int FGetActiveComponent(uint dwgac, out IOleComponent ppic, OLECRINFO[] pcrinfo, uint dwReserved)
        {
            throw new NotImplementedException();
        }

        public int FGetParentComponentManager(out IOleComponentManager ppicm)
        {
            throw new NotImplementedException();
        }

        public int FInState(uint uStateID, IntPtr pvoid)
        {
            throw new NotImplementedException();
        }

        public int FOnComponentActivate(uint dwComponentID)
        {
            throw new NotImplementedException();
        }

        public int FOnComponentExitState(uint dwComponentID, uint uStateID, uint uContext, uint cpicmExclude, IOleComponentManager[] rgpicmExclude)
        {
            throw new NotImplementedException();
        }

        public int FPushMessageLoop(uint dwComponentID, uint uReason, IntPtr pvLoopData)
        {
            throw new NotImplementedException();
        }

        public int FRegisterComponent(IOleComponent piComponent, OLECRINFO[] pcrinfo, out uint pdwComponentID)
        {
            var flags = (_OLECRF)pcrinfo[0].grfcrf;
            if (flags.HasFlag(_OLECRF.olecrfNeedIdleTime))
            {
                _idleComponents[++_idleCount] = piComponent;
                pdwComponentID = _idleCount;
            }
            else
            {
                throw new NotImplementedException();
            }
            return VSConstants.S_OK;
        }

        public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam)
        {
            throw new NotImplementedException();
        }

        public int FRevokeComponent(uint dwComponentID)
        {
            _idleComponents.Remove(dwComponentID);
            return VSConstants.S_OK;
        }

        public int FSetTrackingComponent(uint dwComponentID, int fTrack)
        {
            throw new NotImplementedException();
        }

        public int FUpdateComponentRegistration(uint dwComponentID, OLECRINFO[] pcrinfo)
        {
            throw new NotImplementedException();
        }

        public void OnComponentEnterState(uint dwComponentID, uint uStateID, uint uContext, uint cpicmExclude, IOleComponentManager[] rgpicmExclude, uint dwReserved)
        {
            throw new NotImplementedException();
        }

        public void QueryService(ref Guid guidService, ref Guid iid, out IntPtr ppvObj)
        {
            throw new NotImplementedException();
        }
    }
}
