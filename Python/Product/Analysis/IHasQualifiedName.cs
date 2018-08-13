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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    interface IHasQualifiedName {
        /// <summary>
        /// Gets the fully qualified, dot-separated name of the value.
        /// This is typically used for displaying to users.
        /// </summary>
        string FullyQualifiedName { get; }

        /// <summary>
        /// Gets the import and lookup names of the value. The first part
        /// should be importable, and the second is a name that can be
        /// resolved with getattr().
        /// These are often seen separated with a colon.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// The value cannot be resolved (for example, a nested function).
        /// </exception>
        KeyValuePair<string, string> FullyQualifiedNamePair { get; }
    }

    static class QualifiedNameExtensions {
        public static string CombineNames(this KeyValuePair<string, string> qualifiedNamePair, string sep = ".") {
            if (string.IsNullOrEmpty(qualifiedNamePair.Key)) {
                return qualifiedNamePair.Value;
            }
            return qualifiedNamePair.Key + sep + qualifiedNamePair.Value;
        }
    }
}
