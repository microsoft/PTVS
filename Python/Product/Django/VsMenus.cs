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
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PythonTools.Django {
    /// <summary>
    /// Defines menu commands guids and menu command id's
    /// </summary>
    class VsMenus {
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
