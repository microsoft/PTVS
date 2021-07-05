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

using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Represents an individual rename location within a file in the refactor preview window.
    /// </summary>
    class LocationPreviewItem : IPreviewItem {
        private readonly FilePreviewItem _parent;
        private readonly string _text;
        private Span _span;
        private bool _checked = true;
        private static readonly char[] _whitespace = new[] { ' ', '\t', '\f' };

        private LocationPreviewItem(FilePreviewItem parent, string text, int line, int column, Span span) {
            _parent = parent;
            _text = text;
            Line = line;
            Column = column;
            _span = span;
        }

        public static LocationPreviewItem Create(VsProjectAnalyzer analyzer, FilePreviewItem parent, LocationInfo locationInfo, VariableType type) {
            Debug.Assert(locationInfo.StartColumn >= 1, "Invalid location info (Column)");
            Debug.Assert(locationInfo.StartLine >= 1, "Invalid location info (Line)");

            var origName = parent?.Engine?.OriginalName;
            if (string.IsNullOrEmpty(origName)) {
                return null;
            }

            var analysis = analyzer.GetAnalysisEntryFromUri(locationInfo.DocumentUri) ??
                analyzer.GetAnalysisEntryFromPath(locationInfo.FilePath);
            if (analysis == null) {
                return null;
            }

            var text = analysis.GetLine(locationInfo.StartLine);
            if (string.IsNullOrEmpty(text)) {
                return null;
            }

            int start, length;
            if (!GetSpan(text, origName, locationInfo, out start, out length)) {
                // Name does not match exactly, so we might be renaming a prefixed name
                var prefix = parent.Engine.PrivatePrefix;
                if (string.IsNullOrEmpty(prefix)) {
                    // No prefix available, so don't rename this
                    return null;
                }

                var newName = parent.Engine.Request.Name;
                if (string.IsNullOrEmpty(newName)) {
                    // No incoming name
                    Debug.Fail("No incoming name");
                    return null;
                }

                if (!GetSpanWithPrefix(text, origName, locationInfo, prefix, newName, out start, out length)) {
                    // Not renaming a prefixed name
                    return null;
                }
            }

            if (start < 0 || length <= 0) {
                Debug.Fail("Expected valid span");
                return null;
            }

            var trimText = text.TrimStart(_whitespace);
            return new LocationPreviewItem(
                parent,
                trimText,
                locationInfo.StartLine,
                start + 1,
                new Span(start - (text.Length - trimText.Length), length)
            );
        }

        private static bool GetSpan(string text, string origName, LocationInfo loc, out int start, out int length) {
            if (string.IsNullOrEmpty(text)) {
                throw new ArgumentNullException(nameof(text));
            }
            if (string.IsNullOrEmpty(text)) {
                throw new ArgumentNullException(nameof(origName));
            }

            start = loc.StartColumn - 1;
            length = (loc.EndLine == loc.StartLine ? loc.EndColumn - loc.StartColumn : null) ?? origName.Length;
            if (start < 0 || length <= 0) {
                Debug.Fail("Invalid span for '{0}': [{1}..{2})".FormatInvariant(origName, start, start + length));
                return false;
            }

            var cmp = CultureInfo.InvariantCulture.CompareInfo;
            try {
                if (length == origName.Length && cmp.Compare(text, start, length, origName, 0, origName.Length) == 0) {
                    // Name matches, so return the span
                    return true;
                }
            } catch (ArgumentOutOfRangeException ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(LocationPreviewItem)));
            }

            start = -1;
            length = -1;
            return false;
        }

        private static bool GetSpanWithPrefix(string text, string origName, LocationInfo loc, string prefix, string newName, out int start, out int length) {
            if (string.IsNullOrEmpty(prefix)) {
                throw new ArgumentNullException(nameof(prefix));
            }
            if (string.IsNullOrEmpty(newName)) {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (!GetSpan(text, prefix + origName, loc, out start, out length)) {
                start = -1;
                length = -1;
                return false;
            }

            if (newName.StartsWithOrdinal("__") && newName.Length > 2) {
                // renaming from private name to private name, so just rename the non-prefixed portion
                start += prefix.Length;
                length -= prefix.Length;
            }

            return true;
        }

        public int Line { get; }
        public int Column { get; }
        public VariableType Type { get; }

        public int Length => _span.Length;


        public ushort Glyph {
            get { return (ushort)StandardGlyphGroup.GlyphGroupField; }
        }

        public IntPtr ImageList {
            get { return IntPtr.Zero; }
        }

        public bool IsExpandable {
            get { return false; }
        }

        public PreviewList Children {
            get { return null; }
        }

        public string GetText(VisualStudio.Shell.Interop.VSTREETEXTOPTIONS options) {
            return _text;
        }

        public _VSTREESTATECHANGEREFRESH ToggleState() {
            var oldParentState = _parent.CheckState;
            _checked = !_checked;

            _parent.UpdateTempFile();

            var newParentState = _parent.CheckState;
            if (oldParentState != newParentState) {
                return _VSTREESTATECHANGEREFRESH.TSCR_PARENTS | _VSTREESTATECHANGEREFRESH.TSCR_CURRENT;
            }

            return _VSTREESTATECHANGEREFRESH.TSCR_CURRENT;
        }

        public __PREVIEWCHANGESITEMCHECKSTATE CheckState {
            get {
                return _checked ? __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Checked : __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Unchecked;
            }
        }

        public void DisplayPreview(IVsTextView view) {
            _parent.DisplayPreview(view);

            var span = new TextSpan();
            span.iEndLine = span.iStartLine = Line - 1;
            span.iStartIndex = Column - 1;
            span.iEndIndex = Column - 1 + _parent.Engine.Request.Name.Length;

            view.EnsureSpanVisible(span);
        }

        public void Close(VSTREECLOSEACTIONS vSTREECLOSEACTIONS) {
        }

        public Span? Selection {
            get {
                return _span;
            }
        }

    }
}
