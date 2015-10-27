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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Navigation {
    class SimpleObject : ISimpleObject {
        #region ISimpleObject Members

        public virtual bool CanDelete {
            get { return false; }
        }

        public virtual bool CanGoToSource {
            get { return false; }
        }

        public virtual bool CanRename {
            get { return false; }
        }

        public virtual string Name {
            get { return String.Empty; }
        }

        public virtual string UniqueName {
            get { return String.Empty; }
        }

        public virtual string FullName {
            get {
                return Name;
            }
        }

        public virtual string GetTextRepresentation(VSTREETEXTOPTIONS options) {
            return Name;
        }

        public virtual string TooltipText {
            get { return String.Empty; }
        }

        public virtual object BrowseObject {
            get { return null; }
        }

        public virtual System.ComponentModel.Design.CommandID ContextMenuID {
            get { return null; }
        }

        public virtual VSTREEDISPLAYDATA DisplayData {
            get { return new VSTREEDISPLAYDATA(); }
        }

        public virtual uint CategoryField(LIB_CATEGORY lIB_CATEGORY) {
            return 0;
        }

        public virtual void Delete() {
        }

        public virtual void DoDragDrop(OleDataObject dataObject, uint grfKeyState, uint pdwEffect) {
        }

        public virtual void Rename(string pszNewName, uint grfFlags) {
        }

        public virtual void GotoSource(VSOBJGOTOSRCTYPE SrcType) {
        }

        public virtual void SourceItems(out IVsHierarchy ppHier, out uint pItemid, out uint pcItems) {
            ppHier = null;
            pItemid = 0;
            pcItems = 0;
        }

        public virtual uint EnumClipboardFormats(_VSOBJCFFLAGS _VSOBJCFFLAGS, VSOBJCLIPFORMAT[] rgcfFormats) {
            return 0;
        }

        public virtual void FillDescription(_VSOBJDESCOPTIONS _VSOBJDESCOPTIONS, IVsObjectBrowserDescription3 pobDesc) {
        }

        public virtual IVsSimpleObjectList2 FilterView(uint ListType) {
            return null;
        }

        #endregion
    }
}
