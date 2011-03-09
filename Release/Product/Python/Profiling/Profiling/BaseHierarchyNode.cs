/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Base class for boilerplate IVsUIHierarchy implementation.  
    /// 
    /// The minimal implementation needs to implement GetProperty.
    /// </summary>
    abstract class BaseHierarchyNode : IVsUIHierarchy {
        internal ServiceProvider _serviceProvider;
        private Dictionary<uint, IVsHierarchyEvents> _events = new Dictionary<uint, IVsHierarchyEvents>();
        private uint _eventCounter;

        #region IVsUIHierarchy Members

        public abstract int GetProperty(uint itemid, int propid, out object pvar);

        public virtual int GetCanonicalName(uint itemid, out string pbstrName) {
            pbstrName = null;
            return VSConstants.E_NOTIMPL;
        }

        public virtual int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie) {
            pdwCookie = _eventCounter;
            _events[_eventCounter++] = pEventSink;
            return VSConstants.S_OK;
        }

        public virtual int Close() {
            return VSConstants.S_OK;
        }

        public virtual int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public virtual int GetGuidProperty(uint itemid, int propid, out Guid pguid) {
            pguid = Guid.Empty;
            return VSConstants.DISP_E_MEMBERNOTFOUND;
        }

        public virtual int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested) {
            ppHierarchyNested = IntPtr.Zero;
            pitemidNested = 0;
            return VSConstants.E_NOTIMPL;
        }

        public virtual int GetSite(out VisualStudio.OLE.Interop.IServiceProvider ppSP) {
            ppSP = _serviceProvider.GetService(typeof(VisualStudio.OLE.Interop.IServiceProvider)) as VisualStudio.OLE.Interop.IServiceProvider;
            return VSConstants.S_OK;
        }

        public virtual int ParseCanonicalName(string pszName, out uint pitemid) {
            pitemid = 0;
            return VSConstants.E_NOTIMPL;
        }

        public virtual int QueryClose(out int pfCanClose) {
            pfCanClose = 1;
            return VSConstants.S_OK;
        }

        public virtual int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, Microsoft.VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText) {
            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_UNKNOWNGROUP;
        }

        public virtual int SetGuidProperty(uint itemid, int propid, ref Guid rguid) {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int SetProperty(uint itemid, int propid, object var) {
            return VSConstants.E_NOTIMPL;
        }

        public virtual int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp) {
            _serviceProvider = new ServiceProvider(psp, true);
            return VSConstants.S_OK;
        }

        public virtual int UnadviseHierarchyEvents(uint dwCookie) {
            _events.Remove(dwCookie);
            return VSConstants.S_OK;
        }

        public int Unused0() {
            throw new NotImplementedException();
        }

        public int Unused1() {
            throw new NotImplementedException();
        }

        public int Unused2() {
            throw new NotImplementedException();
        }

        public int Unused3() {
            throw new NotImplementedException();
        }

        public int Unused4() {
            throw new NotImplementedException();
        }

        #endregion

        protected void OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded) {
            foreach(var ev in _events.Values) {
                ev.OnItemAdded(itemidParent, itemidSiblingPrev, itemidAdded);                
            }
        }

        protected void OnItemDeleted(uint itemid) {
            foreach (var ev in _events.Values) {
                ev.OnItemDeleted(itemid);
            }
        }

        protected void OnInvalidateItems(uint itemidParent) {            
            foreach (var ev in _events.Values) {
                ev.OnInvalidateItems(itemidParent);
            }
        }

        protected void OnPropertyChanged(uint itemid, int propid, uint flags) {
            foreach (var ev in _events.Values) {
                ev.OnPropertyChanged(itemid, propid, flags);
            }
        }
    }
}
