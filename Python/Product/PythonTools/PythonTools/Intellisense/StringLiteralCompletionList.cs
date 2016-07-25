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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.InteractiveWindow;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides file path completion
    /// </summary>
    internal class StringLiteralCompletionList : CompletionAnalysis {
        internal StringLiteralCompletionList(IServiceProvider serviceProvider, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(serviceProvider, session, view, span, textBuffer, options) {
        }

        private static readonly char[] QuoteChars = new[] { '"', '\'' };
        private static readonly char[] SepChars = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        private static bool PathStartsWith(string path, string prefix) {
            int sep = path.IndexOfAny(SepChars);
            if (sep < prefix.Length) {
                return false;
            }
            return path.Substring(0, sep) == prefix;
        }

        private static string RestorePrefix(string path, string prefix, string prefixRepl) {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                return path;
            }
            return prefixRepl + path.Substring(prefix.Length);
        }

        internal struct EntryInfo {
            public string Caption;
            public string InsertionText;
            public string Tooltip;
            public bool IsFile;

            public override bool Equals(object obj) {
                if (!(obj is EntryInfo)) {
                    return false;
                }
                var other = (EntryInfo)obj;
                return Caption == other.Caption &&
                    InsertionText == other.InsertionText &&
                    Tooltip == other.Tooltip &&
                    IsFile == other.IsFile;
            }

            public override int GetHashCode() {
                return Tooltip.GetHashCode();
            }

            public override string ToString() {
                return string.Format("<{0}>", InsertionText);
            }
        }

        internal static IEnumerable<EntryInfo> GetEntryInfo(string text, string cwd, string user) {
            var dir = text;

            int start = text.IndexOfAny(QuoteChars);
            if (start >= 0) {
                var quote = text[start];
                dir = text.Substring(start + 1);
                if (string.IsNullOrEmpty(dir)) {
                    return Enumerable.Empty<EntryInfo>();
                }
                if (dir[dir.Length - 1] == quote) {
                    dir = dir.Remove(dir.Length - 1);
                }
            }

            string prefix = "", prefixReplacement = "";

            if (Path.IsPathRooted(dir)) {
            } else if (PathStartsWith(dir, ".")) {
                prefixReplacement = ".\\";
                prefix = PathUtils.EnsureEndSeparator(cwd);
                dir = prefix + dir.Substring(2);
            } else if (PathStartsWith(dir, "~")) {
                prefixReplacement = "~\\";
                prefix = PathUtils.EnsureEndSeparator(user);
                dir = prefix + dir.Substring(2);
            } else {
                return Enumerable.Empty<EntryInfo>();
            }
            if (!Directory.Exists(dir)) {
                dir = PathUtils.GetParent(dir);
                if (!Directory.Exists(dir)) {
                    return Enumerable.Empty<EntryInfo>();
                }
            }

            var dirs = PathUtils.EnumerateDirectories(dir, recurse: false, fullPaths: true)
                .Select(d => new EntryInfo {
                    Caption = PathUtils.GetFileOrDirectoryName(d),
                    InsertionText = "r\"" + RestorePrefix(d, prefix, prefixReplacement) + "\\\"",
                    Tooltip = d,
                    IsFile = false
                });
            var files = PathUtils.EnumerateFiles(dir, recurse: false, fullPaths: true)
                .Select(f => new EntryInfo {
                    Caption = PathUtils.GetFileOrDirectoryName(f),
                    InsertionText = "r\"" + RestorePrefix(f, prefix, prefixReplacement) + "\"",
                    Tooltip = f,
                    IsFile = true
                });

            return dirs.Concat(files);
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var snapshot = TextBuffer.CurrentSnapshot;
            var span = snapshot.GetApplicableSpan(Span.GetStartPoint(snapshot));
            var text = span.GetText(snapshot);

            string cwd;
            var replEval = TextBuffer.GetInteractiveWindow()?.GetPythonEvaluator();
            if (replEval != null) {
                cwd = replEval.CurrentWorkingDirectory;
            } else if (!string.IsNullOrEmpty(cwd = TextBuffer.GetFilePath())) {
                cwd = PathUtils.GetParent(cwd);
            } else {
                cwd = Environment.CurrentDirectory;
            }

            var completions = GetEntryInfo(text, cwd, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                .Select(e => new DynamicallyVisibleCompletion(
                    e.Caption,
                    e.InsertionText,
                    e.Tooltip,
                    glyphService.GetGlyph(
                        e.IsFile ? StandardGlyphGroup.GlyphLibrary : StandardGlyphGroup.GlyphClosedFolder,
                        StandardGlyphItem.GlyphItemPublic
                    ),
                    e.IsFile ? "File" : "Directory"
                )).ToArray();

            if (completions.Length == 0) {
                return null;
            }

            Session.Committed += Session_Committed;

            return new FuzzyCompletionSet(
                "PythonFilenames",
                "Files",
                Span,
                completions,
                _options,
                CompletionComparer.UnderscoresLast,
                matchInsertionText: true
            );
        }

        private void Session_Committed(object sender, EventArgs e) {
            View.Caret.MoveTo(View.Caret.Position.BufferPosition.Subtract(1));
        }
    }
}
