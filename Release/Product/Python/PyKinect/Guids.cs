/* 
 * ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 * for more information.
 *
 * ***************************************************************************/

// Guids.cs
// MUST match guids.h
using System;

namespace Microsoft.Samples
{
    static class GuidList
    {
        public const string guidPyKinectPkgString = "83f5123e-4b4a-45c0-baec-bea8964d7b25";
        public const string guidPyKinectCmdSetString = "521747c2-b632-4367-bcf8-b0c0806a1d4a";

        public static readonly Guid guidPyKinectCmdSet = new Guid(guidPyKinectCmdSetString);
    };
}