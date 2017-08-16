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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides file path completion
    /// </summary>
    internal class StringLiteralCompletionList : CompletionAnalysis {
        private const int MaxItems = 10000;

        internal StringLiteralCompletionList(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {
        }

        internal static readonly char[] QuoteChars = new[] { '"', '\'' };
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

        internal static Span? GetStringContentSpan(string text, int globalStart = 0) {
            int firstQuote = text.IndexOfAny(QuoteChars);
            int lastQuote = text.LastIndexOfAny(QuoteChars) - 1;
            if (firstQuote < 0 || lastQuote < 0) {
                return null;
            }
            if (firstQuote + 2 < text.Length &&
                text[firstQuote + 1] == text[firstQuote] && text[firstQuote + 2] == text[firstQuote]) {
                firstQuote += 2;

                lastQuote -= 2;
            }

            return new Span(
                globalStart + firstQuote + 1,
                ((lastQuote >= firstQuote) ? lastQuote : text.Length) - firstQuote
            );
        }

        internal static bool CanComplete(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            var span = GetStringContentSpan(text) ?? new Span(0, text.Length);
            if (span.End > text.Length) {
                return false;
            }

            text = text.Substring(span.Start, span.Length);

            return (PathUtils.IsValidPath(text) && Path.IsPathRooted(text) && text.IndexOfAny(SepChars) > 0) ||
                PathStartsWith(text, ".") ||
                PathStartsWith(text, "~");
        }

        internal static bool GetDirectoryAndPrefix(
            string text,
            string cwd,
            string user,
            out string dir,
            out string prefix,
            out string prefixReplacement
        ) {
            prefix = "";
            prefixReplacement = "";

            var span = GetStringContentSpan(text) ?? new Span(0, text.Length);
            dir = text.Substring(span.Start, span.Length);

            if (!PathUtils.IsValidPath(dir)) {
                return false;
            }

            if (Path.IsPathRooted(dir) && dir.IndexOfAny(SepChars) > 0) {
            } else if (PathStartsWith(dir, ".")) {
                prefixReplacement = ".\\";
                prefix = PathUtils.EnsureEndSeparator(cwd);
                dir = prefix + dir.Substring(2);
            } else if (PathStartsWith(dir, "~")) {
                prefixReplacement = "~\\";
                prefix = PathUtils.EnsureEndSeparator(user);
                dir = prefix + dir.Substring(2);
            } else {
                return false;
            }
            if (!Directory.Exists(dir)) {
                if (PathUtils.HasEndSeparator(dir)) {
                    return false;
                }
                dir = PathUtils.GetParent(dir);
                if (!Directory.Exists(dir)) {
                    return false;
                }
            }

            return true;
        }

        internal static IEnumerable<EntryInfo> GetEntryInfo(string text, string cwd, string user) {
            string dir, prefix, prefixReplacement;
            if (!GetDirectoryAndPrefix(text, cwd, user, out dir, out prefix, out prefixReplacement)) {
                return Enumerable.Empty<EntryInfo>();
            }

            var dirNames = PathUtils.EnumerateDirectories(dir, recurse: false, fullPaths: false).ToList();
            var fileNames = PathUtils.EnumerateFiles(dir, recurse: false, fullPaths: false).ToList();

            if (dirNames.Count + fileNames.Count > MaxItems) {
                Debug.Write($"Found {dirNames.Count} dirs and {fileNames.Count} files");
                var filter = PathUtils.GetFileOrDirectoryName(text);
                if (!string.IsNullOrEmpty(filter)) {
                    Debug.WriteLine($"Filtering filenames with '{filter}'");
                    dirNames.RemoveAll(d => !d.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
                    fileNames.RemoveAll(f => !f.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
                }
            }
            if (dirNames.Count + fileNames.Count > MaxItems) {
                Debug.Write($"Aborting due to {dirNames.Count} dirs and {fileNames.Count} files");
                return Enumerable.Empty<EntryInfo>();
            }

            var prefixedRoot = PathUtils.EnsureEndSeparator(RestorePrefix(dir, prefix, prefixReplacement));
            var root = PathUtils.EnsureEndSeparator(dir);

            var dirs = dirNames
                .Select(d => new EntryInfo {
                    Caption = d,
                    InsertionText = prefixedRoot + d + "\\",
                    Tooltip = root + d,
                    IsFile = false
                });
            var files = fileNames
                .Select(f => new EntryInfo {
                    Caption = f,
                    InsertionText = prefixedRoot + f,
                    Tooltip = root + f,
                    IsFile = true
                });

            return dirs.Concat(files);
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var snapshot = TextBuffer.CurrentSnapshot;
            var span = snapshot.GetApplicableSpan(Span.GetStartPoint(snapshot));
            var classifier = snapshot.TextBuffer.GetPythonClassifier();
            if (span == null || classifier == null) {
                // Not a valid Python text buffer, which should not happen
                Debug.Fail("Getting completions for non-Python buffer");
                return null;
            }

            var text = span.GetText(snapshot);
            var token = classifier.GetClassificationSpans(span.GetSpan(snapshot)).LastOrDefault();
            if (token == null) {
                // Not a valid line of tokens
                return null;
            }
            bool quoteBackslash = !token.Span.GetText().TakeWhile(c => !QuoteChars.Contains(c)).Any(c => c == 'r' || c == 'R');

            if (quoteBackslash) {
                text = text.Replace("\\\\", "\\");
            }

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
                    quoteBackslash ? e.InsertionText.Replace("\\", "\\\\") : e.InsertionText,
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
    }
}
