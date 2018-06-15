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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
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
        public readonly IReadOnlyDictionary<int, BufferVersion> Versions;

        public VersionCookie(int version) {
            Versions = new Dictionary<int, BufferVersion> {
                [0] = new BufferVersion(version, null, null)
            };
        }

        public VersionCookie(IDictionary<int, BufferVersion> versions) {
            Versions = new SortedDictionary<int, BufferVersion>(versions);
        }

        public IEnumerable<KeyValuePair<Uri, BufferVersion>> GetAllParts(Uri documentUri) {
            foreach (var kv in Versions) {
                var u = documentUri;
                if (kv.Key > 0) {
                    u = new Uri(u, $"#{kv.Key}");
                }
                yield return new KeyValuePair<Uri, BufferVersion>(u, kv.Value);
            }
        }

        /// <summary>
        /// Returns the version of the default part (part #0).
        /// </summary>
        public int? DefaultVersion => Versions.TryGetValue(0, out var bv) ? bv.Version : (int?)null;

        /// <summary>
        /// Gets the version data for part identified by the specified URI.
        /// Returns null if not found.
        /// </summary>
        public BufferVersion GetVersion(Uri documentUri) {
            if (documentUri == null) {
                return null;
            }

            BufferVersion result;
            var f = documentUri.Fragment;
            if (!string.IsNullOrEmpty(f) &&
                f.StartsWithOrdinal("#") &&
                int.TryParse(f.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) {
                Versions.TryGetValue(i, out result);
            } else {
                Versions.TryGetValue(0, out result);
            }
            return result;
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
        /// The AST that was produced from the last version parsed
        /// </summary>
        public readonly PythonAst Ast;
        /// <summary>
        /// The diagnostic messages produced for this buffer
        /// </summary>
        public readonly IReadOnlyList<Analysis.LanguageServer.Diagnostic> Diagnostics;

        public BufferVersion(
            int version,
            PythonAst ast,
            IEnumerable<Analysis.LanguageServer.Diagnostic> diagnostics
        ) {
            Version = version;
            Ast = ast;
            Diagnostics = diagnostics.MaybeEnumerate().ToArray();
        }
    }
}
