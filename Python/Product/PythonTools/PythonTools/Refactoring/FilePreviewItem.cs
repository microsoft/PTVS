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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Represents all of the changes to a single file in the refactor preview window.
    /// </summary>
    class FilePreviewItem : IPreviewItem {
        public readonly string Filename;
        private readonly string _tempFileName;
        public readonly List<IPreviewItem> Items = new List<IPreviewItem>();
        private readonly PreviewChangesEngine _engine;
        private readonly List<IVsTextLineMarker> _markers = new List<IVsTextLineMarker>();
        private PreviewList _list;
        private IVsTextLines _buffer;
        private bool _toggling;

        internal static readonly ImageList _imageList = Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList));

        public FilePreviewItem(PreviewChangesEngine engine, string file) {
            Filename = file;
            _engine = engine;
            do {
                _tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()) + ".py";
            } while (File.Exists(_tempFileName));
        }

        public PreviewChangesEngine Engine {
            get {
                return _engine;
            }
        }

        public ushort Glyph {
            get { return (ushort)CommonImageName.File; }
        }

        public IntPtr ImageList {
            get { return _imageList.Handle; }
        }

        public bool IsExpandable {
            get { return true; }
        }

        public PreviewList Children {
            get {
                if (_list == null) {
                    _list = new PreviewList(Items.ToArray());
                }
                return _list;
            }
        }

        public string GetText(VisualStudio.Shell.Interop.VSTREETEXTOPTIONS options) {
            return Path.GetFileName(Filename);
        }

        public _VSTREESTATECHANGEREFRESH ToggleState() {
            _toggling = true;
            try {
                switch (CheckState) {
                    case __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Checked:
                        foreach (var item in Items) {
                            item.ToggleState();
                        }
                        break;
                    case __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Unchecked:
                    case __PREVIEWCHANGESITEMCHECKSTATE.PCCS_PartiallyChecked:
                        foreach (var item in Items) {
                            if (item.CheckState == __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Unchecked) {
                                item.ToggleState();
                            }
                        }
                        break;

                }
            } finally {
                _toggling = false;
            }

            UpdateTempFile();

            return _VSTREESTATECHANGEREFRESH.TSCR_CURRENT | _VSTREESTATECHANGEREFRESH.TSCR_CHILDREN;
        }

        public __PREVIEWCHANGESITEMCHECKSTATE CheckState {
            get {
                __PREVIEWCHANGESITEMCHECKSTATE res = __PREVIEWCHANGESITEMCHECKSTATE.PCCS_None;
                foreach (var child in Items) {
                    if (res == __PREVIEWCHANGESITEMCHECKSTATE.PCCS_None) {
                        res = child.CheckState;
                    } else if (res != child.CheckState) {
                        res = __PREVIEWCHANGESITEMCHECKSTATE.PCCS_PartiallyChecked;
                        break;
                    }
                }
                return res;
            }
        }

        public void DisplayPreview(IVsTextView view) {
            EnsureTempFile();

            // transfer the analyzer to the underlying buffer so we tokenize with the correct version of the language
            var model = (IComponentModel)_engine._serviceProvider.GetService(typeof(SComponentModel));
            var adapterFactory = model.GetService<IVsEditorAdaptersFactoryService>();
            var buffer = adapterFactory.GetDocumentBuffer(_buffer);

            view.SetBuffer(_buffer);
        }

        public void Close(VSTREECLOSEACTIONS vSTREECLOSEACTIONS) {
            if (_tempFileName != null && File.Exists(_tempFileName)) {
                File.Delete(_tempFileName);
            }

            _list.OnClose(new[] { vSTREECLOSEACTIONS });
        }

        public Span? Selection {
            get {
                return null;
            }
        }

        private void EnsureTempFile() {
            if (_buffer == null) {
                CreateTempFile();

                var invisbleFileManager = (IVsInvisibleEditorManager)_engine._serviceProvider.GetService(typeof(SVsInvisibleEditorManager));
                IVsInvisibleEditor editor;
                ErrorHandler.ThrowOnFailure(invisbleFileManager.RegisterInvisibleEditor(_tempFileName, null, (uint)_EDITORREGFLAGS.RIEF_ENABLECACHING, null, out editor));

                IntPtr buffer;
                var guid = typeof(IVsTextLines).GUID;
                ErrorHandler.ThrowOnFailure(editor.GetDocData(0, ref guid, out buffer));
                try {
                    _buffer = Marshal.GetObjectForIUnknown(buffer) as IVsTextLines;
                } finally {
                    if (buffer != IntPtr.Zero) {
                        Marshal.Release(buffer);
                    }
                }

                AddMarkers();
            }
        }

        internal void UpdateTempFile() {
            if (!_toggling) {
                // TODO: This could be much more efficent then re-generating the file every time, we could instead
                // update the existing buffer and markers.
                ClearMarkers();

                CreateTempFile();

                ReloadBuffer();
            }
        }

        interface IFileUpdater {
            void Replace(int lineNo, int column, int lengthToDelete, string newText);
            void Log(string message);
            void Save();
        }

        class TempFileUpdater : IFileUpdater {
            private readonly string _tempFile;
            private readonly string[] _lines;

            public TempFileUpdater(string tempFile, ITextBuffer buffer) {
                _tempFile = tempFile;

                _lines = buffer.CurrentSnapshot.Lines.Select(x => x.GetText()).ToArray();
            }

            public void Replace(int lineNo, int column, int lengthToDelete, string newText) {
                var oldLine = _lines[lineNo];
                string newLine = oldLine.Remove(column, lengthToDelete);
                newLine = newLine.Insert(column, newText);
                _lines[lineNo] = newLine;
            }

            public void Save() {
                File.WriteAllLines(_tempFile, _lines);
            }


            public void Log(string message) {
            }
        }

        class BufferUpdater : IFileUpdater {
            private readonly ITextBuffer _buffer;
            private PreviewChangesEngine _engine;

            public BufferUpdater(ITextBuffer buffer, PreviewChangesEngine engine) {
                _buffer = buffer;
                _engine = engine;
            }

            public void Replace(int lineNo, int column, int lengthToDelete, string newText) {
                var line = _buffer.CurrentSnapshot.GetLineFromLineNumber(lineNo);
                _buffer.Replace(new Span(line.Start + column, lengthToDelete), newText);
            }

            public void Save() {
            }


            public void Log(string message) {
                _engine._input.OutputLog(message);
            }
        }

        internal void UpdateBuffer(ITextBuffer buffer) {
            UpdateFile(new BufferUpdater(buffer, _engine));
        }

        private void CreateTempFile() {
            IFileUpdater updater = new TempFileUpdater(_tempFileName, _engine._input.GetBufferForDocument(Filename));

            UpdateFile(updater);
        }

        private void UpdateFile(IFileUpdater updater) {
            for (int i = Items.Count - 1; i >= 0; i--) {
                LocationPreviewItem item = (LocationPreviewItem)Items[i];
                if (item.CheckState == __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Checked) {
                    // change is applied
                    updater.Replace(item.Line - 1, item.Column - 1, item.Length, Engine.Request.Name);

                    updater.Log(Strings.RefactorPreviewUpdatedLogEntry.FormatUI(Filename, item.Line, item.Column, item.Type == Analysis.VariableType.Definition ? Strings.RefactorPreviewUpdatedLogEntryDefinition : Strings.RefactorPreviewUpdatedLogEntryReference));
                }
            }

            updater.Save();
        }

        private void ClearMarkers() {
            foreach (var marker in _markers) {
                ErrorHandler.ThrowOnFailure(marker.Invalidate());
            }
            _markers.Clear();
        }

        private void ReloadBuffer() {
            if (_buffer != null) {
                _buffer.Reload(1);

                AddMarkers();
            }
        }

        private void AddMarkers() {
            int curLine = -1, columnDelta = 0;
            foreach (LocationPreviewItem item in Items) {

                if (item.CheckState == __PREVIEWCHANGESITEMCHECKSTATE.PCCS_Checked) {
                    if (item.Line != curLine) {
                        columnDelta = 0;
                    }
                    curLine = item.Line;

                    IVsTextLineMarker[] marker = new IVsTextLineMarker[1];

                    ErrorHandler.ThrowOnFailure(
                        _buffer.CreateLineMarker(
                            (int)MARKERTYPE2.MARKER_REFACTORING_FIELD,
                            item.Line - 1,
                            item.Column - 1 + columnDelta,
                            item.Line - 1,
                            item.Column - 1 + Engine.Request.Name.Length + columnDelta,
                            null,
                            marker
                        )
                    );

                    columnDelta += Engine.Request.Name.Length - Engine.OriginalName.Length;
                    _markers.Add(marker[0]);
                }
            }
        }
    }
}
