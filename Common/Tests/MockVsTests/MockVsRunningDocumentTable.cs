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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.MockVsTests {
    class MockVsRunningDocumentTable : IVsRunningDocumentTable {
        private readonly MockVs _vs;
        private readonly Dictionary<uint, DocInfo> _table = new Dictionary<uint, DocInfo>();
        private readonly Dictionary<string, uint> _ids = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        private uint _curCookie;

        public MockVsRunningDocumentTable(MockVs vs) {
            _vs = vs;
        }

        class DocInfo {
            public _VSRDTFLAGS Flags;
            public string Document;
            public IVsHierarchy Hierarchy;
            public uint ItemId;
            public IntPtr DocData;
            public uint Cookie;
            public int LockCount = 1;

            public DocInfo(_VSRDTFLAGS _VSRDTFLAGS, string pszMkDocument, IVsHierarchy pHier, uint itemid, IntPtr punkDocData, uint p) {
                Flags = _VSRDTFLAGS;
                Document = pszMkDocument;
                Hierarchy = pHier;
                ItemId = itemid;
                DocData = punkDocData;
                Cookie = p;
            }
        }

        public int AdviseRunningDocTableEvents(IVsRunningDocTableEvents pSink, out uint pdwCookie) {
            throw new NotImplementedException();
        }

        public int FindAndLockDocument(uint dwRDTLockType, string pszMkDocument, out IVsHierarchy ppHier, out uint pitemid, out IntPtr ppunkDocData, out uint pdwCookie) {
            uint id;
            if (_ids.TryGetValue(pszMkDocument, out id)) {
                var docInfo = _table[id];
                var lockType = (_VSRDTFLAGS)dwRDTLockType;

                ppHier = docInfo.Hierarchy;
                pitemid = docInfo.ItemId;
                if (docInfo.DocData != IntPtr.Zero) {
                    Marshal.AddRef(docInfo.DocData);
                }
                ppunkDocData = docInfo.DocData;
                pdwCookie = id;
                docInfo.Flags = (_VSRDTFLAGS)dwRDTLockType;
                if (lockType.HasFlag(_VSRDTFLAGS.RDT_ReadLock) || lockType.HasFlag(_VSRDTFLAGS.RDT_EditLock)) {
                    docInfo.LockCount++;
                }
                return VSConstants.S_OK;
            }
            ppHier = null;
            pitemid = 0;
            ppunkDocData = IntPtr.Zero;
            pdwCookie = 0;
            return VSConstants.S_FALSE;
        }

        public int GetDocumentInfo(uint docCookie, out uint pgrfRDTFlags, out uint pdwReadLocks, out uint pdwEditLocks, out string pbstrMkDocument, out IVsHierarchy ppHier, out uint pitemid, out IntPtr ppunkDocData) {
            throw new NotImplementedException();
        }

        public int GetRunningDocumentsEnum(out IEnumRunningDocuments ppenum) {
            throw new NotImplementedException();
        }

        public int LockDocument(uint grfRDTLockType, uint dwCookie) {
            DocInfo docInfo;
            if (_table.TryGetValue(dwCookie, out docInfo)) {
                docInfo.LockCount++;
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int ModifyDocumentFlags(uint docCookie, uint grfFlags, int fSet) {
            throw new NotImplementedException();
        }

        public int NotifyDocumentChanged(uint dwCookie, uint grfDocChanged) {
            throw new NotImplementedException();
        }

        public int NotifyOnAfterSave(uint dwCookie) {
            throw new NotImplementedException();
        }

        public int NotifyOnBeforeSave(uint dwCookie) {
            throw new NotImplementedException();
        }

        public int RegisterAndLockDocument(uint grfRDTLockType, string pszMkDocument, IVsHierarchy pHier, uint itemid, IntPtr punkDocData, out uint pdwCookie) {
            pdwCookie = _ids[pszMkDocument] = ++_curCookie;
            _table[pdwCookie] = new DocInfo(
                (_VSRDTFLAGS)grfRDTLockType,
                pszMkDocument,
                pHier,
                itemid,
                punkDocData,
                pdwCookie
            );

            if (punkDocData != IntPtr.Zero) {
                Marshal.AddRef(punkDocData);
            }
            var persist = (IVsPersistDocData)Marshal.GetObjectForIUnknown(punkDocData);
            ErrorHandler.ThrowOnFailure(persist.OnRegisterDocData(pdwCookie, pHier, itemid));
            return VSConstants.S_OK;
        }

        public int RegisterDocumentLockHolder(uint grfRDLH, uint dwCookie, IVsDocumentLockHolder pLockHolder, out uint pdwLHCookie) {
            throw new NotImplementedException();
        }

        public int RenameDocument(string pszMkDocumentOld, string pszMkDocumentNew, IntPtr pHier, uint itemidNew) {
            uint id;
            if (_ids.TryGetValue(pszMkDocumentOld, out id)) {
                DocInfo docInfo = _table[id];

                var docData = (IVsPersistDocData)Marshal.GetObjectForIUnknown(docInfo.DocData);
                int hr = docData.RenameDocData(0, (IVsHierarchy)Marshal.GetObjectForIUnknown(pHier), itemidNew, pszMkDocumentNew);
                if (ErrorHandler.Succeeded(hr)) {
                    docInfo.Document = pszMkDocumentNew;
                    docInfo.Hierarchy = (IVsHierarchy)Marshal.GetObjectForIUnknown(pHier);
                    docInfo.ItemId = itemidNew;
                    return VSConstants.S_OK;
                }
                return hr;
            }
            return VSConstants.E_FAIL;
        }

        public int SaveDocuments(uint grfSaveOpts, IVsHierarchy pHier, uint itemid, uint docCookie) {
            throw new NotImplementedException();
        }

        public int UnadviseRunningDocTableEvents(uint dwCookie) {
            throw new NotImplementedException();
        }

        public int UnlockDocument(uint grfRDTLockType, uint dwCookie) {
            DocInfo docInfo;
            if (_table.TryGetValue(dwCookie, out docInfo)) {
                docInfo.LockCount--;
                if (docInfo.LockCount == 0) {
                    _ids.Remove(docInfo.Document);
                    _table.Remove(dwCookie);
                    ErrorHandler.ThrowOnFailure(((IVsPersistDocData)Marshal.GetObjectForIUnknown(docInfo.DocData)).Close());
                    Marshal.Release(docInfo.DocData);
                }
                return VSConstants.S_OK;
            }
            return VSConstants.E_FAIL;
        }

        public int UnregisterDocumentLockHolder(uint dwLHCookie) {
            throw new NotImplementedException();
        }
    }
}
