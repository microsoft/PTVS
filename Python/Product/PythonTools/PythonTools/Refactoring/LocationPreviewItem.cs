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
using Microsoft.PythonTools.Analysis;
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
        private readonly VariableType _type;
        private readonly int _lineNo, _columnNo;
        private Span _span;
        private bool _checked = true;
        private static readonly char[] _whitespace = new[] { ' ', '\t', '\f' };

        public LocationPreviewItem(VsProjectAnalyzer analyzer, FilePreviewItem parent, AnalysisLocation locationInfo, VariableType type) {
            _lineNo = locationInfo.Line;
            _columnNo = locationInfo.Column;            
            _parent = parent;
            var analysis = analyzer.GetAnalysisEntryFromPath(locationInfo.FilePath);
            _type = type;
            if (analysis != null) {
                string text = analysis.GetLine(locationInfo.Line);
                string trimmed = text.TrimStart(_whitespace);
                _text = trimmed;
                _span = new Span(_columnNo - (text.Length - trimmed.Length) - 1, parent.Engine.OriginalName.Length);
                if (String.Compare(_text, _span.Start, parent.Engine.OriginalName, 0, parent.Engine.OriginalName.Length) != 0) {
                    // we are renaming a name mangled name (or we have a bug where the names aren't lining up).
                    Debug.Assert(_text.Substring(_span.Start, _span.Length + 1 + parent.Engine.PrivatePrefix.Length) == "_" + parent.Engine.PrivatePrefix + parent.Engine.OriginalName);


                    if (parent.Engine.Request.Name.StartsWith("__")) {
                        // if we're renaming to a private prefix name then we just rename the non-prefixed portion
                        _span = new Span(_span.Start + 1 + parent.Engine.PrivatePrefix.Length, _span.Length);
                        _columnNo += 1 + parent.Engine.PrivatePrefix.Length;
                    } else {
                        // otherwise we renmae the prefixed and non-prefixed portion
                        _span = new Span(_span.Start, _span.Length + 1 + parent.Engine.PrivatePrefix.Length);
                    }
                }
            } else {
                _text = String.Empty;
            }
        }

        public int Line {
            get {
                return _lineNo;
            }
        }

        public int Column {
            get {
                return _columnNo;
            }
        }

        public int Length {
            get {
                return _span.Length;
            }
        }

        public VariableType Type {
            get {
                return _type;
            }
        }

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
