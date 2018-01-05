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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Tracks version information for a parsed project entry along with
    /// the individual ASTs which represent each unique buffer which forms
    /// an overall project entry.
    /// </summary>
    sealed class VersionCookie : IAnalysisCookie {
        /// <summary>
        /// Dictionary from buffer ID to VersionInfo.
        /// </summary>
        public readonly int Version;

        public VersionCookie(int version) {
            Version = version;
        }
    }

    /// <summary>
    /// Stores a snapshot of a specific buffer at a specific version.
    /// </summary>
    sealed class BufferVersion {
        /// <summary>
        /// The version this buffer was last parsed at
        /// </summary>
        public readonly int Version;
        /// <summary>
        /// The ASP that was produced from the last version parsed
        /// </summary>
        public readonly PythonAst Ast;

        public BufferVersion(int version, PythonAst ast) {
            Version = version;
            Ast = ast;
        }
    }
}
