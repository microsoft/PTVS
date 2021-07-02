// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Django
{
    /// <summary>
    /// Defines menu commands guids and menu command id's
    /// </summary>
    class VsMenus
    {
        public static Guid guidSHLMainMenu = new Guid(0xd309f791, 0x903f, 0x11d0, 0x9e, 0xfc, 0x00, 0xa0, 0xc9, 0x11, 0x00, 0x4f);

        public static Guid guidVsUIHierarchyWindowCmds = new Guid("60481700-078B-11D1-AAF8-00A0C9055A90");

        // Special Menus.
        public const int IDM_VS_CTXT_CODEWIN = 0x040D;
        public const int IDM_VS_CTXT_ITEMNODE = 0x0430;
        public const int IDM_VS_CTXT_PROJNODE = 0x0402;
        public const int IDM_VS_CTXT_REFERENCEROOT = 0x0450;
        public const int IDM_VS_CTXT_REFERENCE = 0x0451;
        public const int IDM_VS_CTXT_FOLDERNODE = 0x0431;
        public const int IDM_VS_CTXT_NOCOMMANDS = 0x041A;

        public const int VSCmdOptQueryParameterList = 1;
        public const int IDM_VS_CTXT_XPROJ_MULTIITEM = 0x0419;
        public const int IDM_VS_CTXT_XPROJ_PROJITEM = 0x0417;

        public const int IDM_VS_CTXT_WEBPROJECT = 0x470;
        public const int IDM_VS_CTXT_WEBFOLDER = 0x471;
        public const int IDM_VS_CTXT_WEBITEMNODE = 0x472;
    }
}
