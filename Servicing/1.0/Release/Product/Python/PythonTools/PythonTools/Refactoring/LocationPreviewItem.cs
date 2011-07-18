/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Represents an individual rename location within a file in the refactor preview window.
    /// </summary>
    class LocationPreviewItem : IPreviewItem {
        private readonly LocationInfo _location;
        private readonly FilePreviewItem _parent;
        private readonly string _text;
        private readonly VariableType _type;
        private Span _span;
        private bool _checked = true;
        private static readonly char[] _whitespace = new[] { ' ', '\t', '\f' };

        public LocationPreviewItem(FilePreviewItem parent, LocationInfo locationInfo, VariableType type) {
            _location = locationInfo;
            _parent = parent;
            string text = _location.ProjectEntry.GetLine(_location.Line);
            string trimmed = text.TrimStart(_whitespace);
            _text = trimmed;
            _type = type;
            _span = new Span(_location.Column - (text.Length - trimmed.Length) - 1, parent.Engine.OriginalName.Length);
        }

        public LocationInfo Location {
            get {
                return _location;
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
            span.iEndLine = span.iStartLine = _location.Line - 1;
            span.iStartIndex = _location.Column - 1;
            span.iEndIndex = _location.Column - 1 + _parent.Engine.Request.Name.Length;

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
