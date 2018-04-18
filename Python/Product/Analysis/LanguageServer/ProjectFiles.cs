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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    sealed class ProjectFiles {
        private readonly ConcurrentDictionary<Uri, IProjectEntry> _projectFiles = new ConcurrentDictionary<Uri, IProjectEntry>();

        public void Clear() => _projectFiles.Clear();
        public IProjectEntry GetOrAddEntry(Uri documentUri, IProjectEntry entry) => _projectFiles.GetOrAdd(documentUri, entry);
        public IProjectEntry RemoveEntry(Uri documentUri) => _projectFiles.TryRemove(documentUri, out var entry) ? entry : null;
        public IEnumerable<IProjectEntry> All => _projectFiles.Values;
        public IEnumerable<string> GetLoadedFiles() => _projectFiles.Keys.Select(k => k.AbsoluteUri);

        public IProjectEntry GetEntry(TextDocumentIdentifier document) => GetEntry(document.uri);
        public IProjectEntry GetEntry(Uri documentUri, bool throwIfMissing = true) {
            IProjectEntry entry = null;
            if ((documentUri == null || !_projectFiles.TryGetValue(documentUri, out entry)) && throwIfMissing) {
                throw new LanguageServerException(LanguageServerException.UnknownDocument, "unknown document");
            }
            return entry;
        }

        public void GetAnalysis(TextDocumentIdentifier document, Position position, int? expectedVersion, out ProjectEntry entry, out PythonAst tree) {
            entry = GetEntry(document) as ProjectEntry;
            if (entry == null) {
                throw new LanguageServerException(LanguageServerException.UnsupportedDocumentType, "unsupported document");
            }
            var parse = entry.GetCurrentParse();
            tree = parse?.Tree;
            if (expectedVersion.HasValue && parse?.Cookie is VersionCookie vc) {
                if (vc.Versions.TryGetValue(GetPart(document.uri), out var bv)) {
                    if (bv.Version != expectedVersion.Value) {
                        throw new LanguageServerException(LanguageServerException.MismatchedVersion, $"document is at version {bv.Version}; expected {expectedVersion.Value}");
                    }
                    tree = bv.Ast;
                }
            }
        }

        internal int GetPart(Uri documentUri) {
            var f = documentUri.Fragment;
            int i;
            if (string.IsNullOrEmpty(f) ||
                !f.StartsWithOrdinal("#") ||
                !int.TryParse(f.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) {
                i = 0;
            }
            return i;
        }
    }
}
