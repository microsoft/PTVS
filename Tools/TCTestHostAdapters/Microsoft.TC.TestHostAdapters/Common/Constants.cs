/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TC.TestHostAdapters
{
    internal static class Constants
    {
        public const string DynamicHostAdapterName = "TC Dynamic";

        public const string MtaHostAdapterName = "TC MTA";

        public const string VsAddinName = "TcVsIdeTestHost";

        public const string IVsIdeTestHostAddinGuidString = "C525A97C-241C-45EF-BE9C-CF95650D9F00";
#if DEV10
        public const string VsIdeTestHostAddinGuidString = "E80282C0-570E-4607-8190-02F30B681921";
#elif DEV11
        public const string VsIdeTestHostAddinGuidString = "32F55E70-9461-4998-827D-C4F9B16A282D";
#elif DEV12
        public const string VsIdeTestHostAddinGuidString = "F993962B-FF17-4B86-88BC-2CFFC457A6FB";
#elif DEV14
        public const string VsIdeTestHostAddinGuidString = "AD2680B2-CBF5-4A51-B760-78691C674DDD";
#else
#error Unrecognized VS Version.
#endif

    }
}
