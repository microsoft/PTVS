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
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// The exception originated by VS Ide Host Adapter.
    /// </summary>
    [Serializable]
    [ComVisible(true)]
    [SuppressMessage("Microsoft.Naming", "CA1706:ShortAcronymsShouldBeUppercase")]  // Be consistent with VS Interop Assemblies.
    public class VsIdeTestHostException : Exception
    {
        public VsIdeTestHostException()
        {
        }

        public VsIdeTestHostException(string message): base(message)
        {
            Debug.Assert(message != null);
        }

        public VsIdeTestHostException(string message, Exception innerException): 
            base(message, innerException)
        {
            Debug.Assert(message != null);
            // innerException can be null.
        }

        /// <summary>
        /// Deserialization constructor. 
        /// Needed for remoting scenarios when exception is propagated from server to the client.
        /// </summary>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        protected VsIdeTestHostException(SerializationInfo info, StreamingContext context): 
            base(info, context)
        {
        }
    }
}
