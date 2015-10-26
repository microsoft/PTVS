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

using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Navigation {
    public interface ISimpleObject {
        bool CanDelete { get; }
        bool CanGoToSource { get; }
        bool CanRename { get; }
        string Name { get; }
        string UniqueName { get; }
        string FullName { get; }
        string GetTextRepresentation(VSTREETEXTOPTIONS options);
        string TooltipText { get; }
        object BrowseObject { get; }
        CommandID ContextMenuID { get; }
        VSTREEDISPLAYDATA DisplayData { get; }

        uint CategoryField(LIB_CATEGORY lIB_CATEGORY);

        void Delete();
        void DoDragDrop(OleDataObject dataObject, uint grfKeyState, uint pdwEffect);
        void Rename(string pszNewName, uint grfFlags);
        void GotoSource(VSOBJGOTOSRCTYPE SrcType);

        void SourceItems(out IVsHierarchy ppHier, out uint pItemid, out uint pcItems);
        uint EnumClipboardFormats(_VSOBJCFFLAGS _VSOBJCFFLAGS, VSOBJCLIPFORMAT[] rgcfFormats);
        void FillDescription(_VSOBJDESCOPTIONS _VSOBJDESCOPTIONS, IVsObjectBrowserDescription3 pobDesc);

        IVsSimpleObjectList2 FilterView(uint ListType);
    }

}
