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

namespace Microsoft.PythonTools.Django {
    static class Utilities {
        /// <summary>
        /// Verifies that two objects represent the same instance of a COM object.
        /// This essentially compares the IUnkown pointers of the 2 objects.
        /// This is needed in scenario where aggregation is involved.
        /// </summary>
        /// <param name="obj1">Can be an object, interface or IntPtr</param>
        /// <param name="obj2">Can be an object, interface or IntPtr</param>
        /// <returns>True if the 2 items represent the same thing</returns>
        public static bool IsSameComObject(object obj1, object obj2) {
            bool isSame = false;
            IntPtr unknown1 = IntPtr.Zero;
            IntPtr unknown2 = IntPtr.Zero;
            try {
                // If we have 2 null, then they are not COM objects and as such "it's not the same COM object"
                if (obj1 != null && obj2 != null) {
                    unknown1 = QueryInterfaceIUnknown(obj1);
                    unknown2 = QueryInterfaceIUnknown(obj2);

                    isSame = IntPtr.Equals(unknown1, unknown2);
                }
            } finally {
                if (unknown1 != IntPtr.Zero) {
                    Marshal.Release(unknown1);
                }

                if (unknown2 != IntPtr.Zero) {
                    Marshal.Release(unknown2);
                }

            }

            return isSame;
        }

        /// <summary>
        /// Retrieve the IUnknown for the managed or COM object passed in.
        /// </summary>
        /// <param name="objToQuery">Managed or COM object.</param>
        /// <returns>Pointer to the IUnknown interface of the object.</returns>
        internal static IntPtr QueryInterfaceIUnknown(object objToQuery) {
            bool releaseIt = false;
            IntPtr unknown = IntPtr.Zero;
            IntPtr result;
            try {
                if (objToQuery is IntPtr) {
                    unknown = (IntPtr)objToQuery;
                } else {
                    // This is a managed object (or RCW)
                    unknown = Marshal.GetIUnknownForObject(objToQuery);
                    releaseIt = true;
                }

                // We might already have an IUnknown, but if this is an aggregated
                // object, it may not be THE IUnknown until we QI for it.
                Guid IID_IUnknown = VSConstants.IID_IUnknown;
                ErrorHandler.ThrowOnFailure(Marshal.QueryInterface(unknown, ref IID_IUnknown, out result));
            } finally {
                if (releaseIt && unknown != IntPtr.Zero) {
                    Marshal.Release(unknown);
                }

            }

            return result;
        }


    }
}
