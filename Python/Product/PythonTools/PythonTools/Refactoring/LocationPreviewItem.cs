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
using System.Diagnostics;
using System.Globalization;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

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

        public LocationPreviewItem(VsProjectAnalyzer analyzer, FilePreviewItem parent, LocationInfo locationInfo, VariableType type) {
            Debug.Assert(locationInfo.StartColumn >= 1, "Invalid location info (Column)");
            Debug.Assert(locationInfo.StartLine >= 1, "Invalid location info (Line)");
            _parent = parent;
            Type = type;
            _text = string.Empty;

            var origName = _parent?.Engine?.OriginalName;
            if (string.IsNullOrEmpty(origName)) {
                return;
            }

            var analysis = analyzer.GetAnalysisEntryFromUri(locationInfo.DocumentUri) ??
                analyzer.GetAnalysisEntryFromPath(locationInfo.FilePath);
            if (analysis == null) {
                return;
            }

            var text = analysis.GetLine(locationInfo.StartLine);
            if (string.IsNullOrEmpty(text)) {
                return;
            }

            int start, length;
            if (!GetSpan(text, origName, locationInfo, out start, out length)) {
                // Name does not match exactly, so we should be renaming a prefixed name
                var prefix = parent.Engine.PrivatePrefix;
                if (string.IsNullOrEmpty(prefix)) {
                    // No prefix available, so fail
                    Debug.Fail("Failed to find '{0}' in '{1}' because we had no private prefix".FormatInvariant(origName, text));
                    return;
                }

                var newName = parent.Engine.Request.Name;
                if (string.IsNullOrEmpty(newName)) {
                    // No incoming name
                    Debug.Fail("No incoming name");
                    return;
                }

                if (!GetSpanWithPrefix(text, origName, locationInfo, prefix, newName, out start, out length)) {
                    // Not renaming a prefixed name
                    Debug.Fail("Failed to find '{0}' in '{1}'".FormatInvariant(origName, text));
                    return;
                }
            }

            if (start < 0 || length <= 0) {
                Debug.Fail("Expected valid span");
                return;
            }

            _text = text.TrimStart(_whitespace);
            Line = locationInfo.StartLine;
            Column = start + 1;
            _span = new Span(start - (text.Length - _text.Length), length);
        }

        private static bool GetSpan(string text, string origName, LocationInfo loc, out int start, out int length) {
            if (string.IsNullOrEmpty(text)) {
                throw new ArgumentNullException(nameof(text));
            }
            if (string.IsNullOrEmpty(text)) {
                throw new ArgumentNullException(nameof(origName));
            }

            start = loc.StartColumn - 1;
            length = origName.Length;
            if (start < 0 || length <= 0) {
                Debug.Fail("Invalid span for '{0}': [{1}..{2})".FormatInvariant(origName, start, start + length));
                return false;
            }

            var cmp = CultureInfo.InvariantCulture.CompareInfo;
            try {
                if (cmp.Compare(text, start, length, origName, 0, origName.Length) == 0) {
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
