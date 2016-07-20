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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
namespace Microsoft.VisualStudioTools {
    using IServiceProvider = System.IServiceProvider;

    /// <summary>
    /// Provides access to Visual Studio's idle processing using a simple .NET event
    /// based API.
    /// 
    /// The IdleManager in instantiated with an IServiceProvider and then the OnIdle
    /// event can be hooked or disconnected as needed.
    /// 
    /// Disposing of the IdleManager will disconnect from Visual Studio idle processing.
    /// </summary>
    sealed class IdleManager : IOleComponent, IDisposable {
        private uint _compId = VSConstants.VSCOOKIE_NIL;
        private readonly IServiceProvider _serviceProvider;
        private IOleComponentManager _compMgr;
        private EventHandler<ComponentManagerEventArgs> _onIdle;

        public IdleManager(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        private void EnsureInit() {
            if (_compId == VSConstants.VSCOOKIE_NIL) {
                lock (this) {
                    if (_compId == VSConstants.VSCOOKIE_NIL) {
                        if (_compMgr == null) {
                            _compMgr = (IOleComponentManager)_serviceProvider.GetService(typeof(SOleComponentManager));
                            OLECRINFO[] crInfo = new OLECRINFO[1];
                            crInfo[0].cbSize = (uint)Marshal.SizeOf(typeof(OLECRINFO));
                            crInfo[0].grfcrf = (uint)_OLECRF.olecrfNeedIdleTime;
                            crInfo[0].grfcadvf = (uint)0;
                            crInfo[0].uIdleTimeInterval = 0;
                            if (ErrorHandler.Failed(_compMgr.FRegisterComponent(this, crInfo, out _compId))) {
                                _compId = VSConstants.VSCOOKIE_NIL;
                            }
                        }
                    }
                }
            }
        }

        #region IOleComponent Members

        public int FContinueMessageLoop(uint uReason, IntPtr pvLoopData, MSG[] pMsgPeeked) {
            return 1;
        }

        public int FDoIdle(uint grfidlef) {
            var onIdle = _onIdle;
            if (onIdle != null) {
                onIdle(this, new ComponentManagerEventArgs(_compMgr));
            }

            return 0;
        }

        internal event EventHandler<ComponentManagerEventArgs> OnIdle {
            add {
                _serviceProvider.GetUIThread().Invoke(() => {
                    EnsureInit();
                    _onIdle += value;
                });
            }
            remove {
                _serviceProvider.GetUIThread().Invoke(() => {
                    EnsureInit();
                    _onIdle -= value;
                });
            }
        }

        public int FPreTranslateMessage(MSG[] pMsg) {
            return 0;
        }

        public int FQueryTerminate(int fPromptUser) {
            return 1;
        }

        public int FReserved1(uint dwReserved, uint message, IntPtr wParam, IntPtr lParam) {
            return 1;
        }

        public IntPtr HwndGetWindow(uint dwWhich, uint dwReserved) {
            return IntPtr.Zero;
        }

        public void OnActivationChange(IOleComponent pic, int fSameComponent, OLECRINFO[] pcrinfo, int fHostIsActivating, OLECHOSTINFO[] pchostinfo, uint dwReserved) {
        }

        public void OnAppActivate(int fActive, uint dwOtherThreadID) {
        }

        public void OnEnterState(uint uStateID, int fEnter) {
        }

        public void OnLoseActivation() {
        }

        public void Terminate() {
        }

        #endregion

        public void Dispose() {
            if (_compId != VSConstants.VSCOOKIE_NIL) {
                _compMgr.FRevokeComponent(_compId);
                _compId = VSConstants.VSCOOKIE_NIL;
            }
        }
    }
}