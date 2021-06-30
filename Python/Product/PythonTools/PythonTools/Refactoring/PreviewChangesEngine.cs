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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Implements our preview changes engine.  Creates a list of all of the preview items based upon the analyzed expression
    /// and rename variable request.
    /// </summary>
    class PreviewChangesEngine : IVsPreviewChangesEngine {
        private readonly string _expr;
        private readonly PreviewList _list;
        internal readonly IRenameVariableInput _input;
        internal readonly VsProjectAnalyzer _analyzer;
        private readonly IEnumerable<AnalysisVariable> _variables;
        internal readonly IServiceProvider _serviceProvider;

        public PreviewChangesEngine(IServiceProvider serviceProvider, IRenameVariableInput input, string expr, RenameVariableRequest request, string originalName, string privatePrefix, VsProjectAnalyzer analyzer, IEnumerable<AnalysisVariable> variables) {
            _serviceProvider = serviceProvider;
            _expr = expr;
            _analyzer = analyzer;
            Request = request;
            OriginalName = originalName;
            PrivatePrefix = privatePrefix;
            _variables = variables;
            _input = input;
            _list = new PreviewList(CreatePreviewItems().ToArray());
        }

        private List<FilePreviewItem> CreatePreviewItems() {
            Dictionary<string, FilePreviewItem> files = new Dictionary<string, FilePreviewItem>();
            Dictionary<FilePreviewItem, HashSet<LocationInfo>> allItems = new Dictionary<FilePreviewItem, HashSet<LocationInfo>>();

            foreach (var variable in _variables) {
                if (string.IsNullOrEmpty(variable.Location?.FilePath)) {
                    continue;
                }

                switch (variable.Type) {
                    case VariableType.Definition:
                    case VariableType.Reference:
                        string file = variable.Location.FilePath;
                        FilePreviewItem fileItem;
                        HashSet<LocationInfo> curLocations;
                        if (!files.TryGetValue(file, out fileItem)) {
                            files[file] = fileItem = new FilePreviewItem(this, file);
                            allItems[fileItem] = curLocations = new HashSet<LocationInfo>(LocationInfo.FullComparer);
                        } else {
                            curLocations = allItems[fileItem];
                        }

                        if (!curLocations.Contains(variable.Location)) {
                            try {
                                var item = LocationPreviewItem.Create(_analyzer, fileItem, variable.Location, variable.Type);
                                if (item != null && item.Length > 0) {
                                    fileItem.Items.Add(item);
                                    curLocations.Add(variable.Location);
                                }
                            } catch (Exception ex) when (!ex.IsCriticalException()) {
                                ex.ReportUnhandledException(_serviceProvider, GetType());
                            }
                        }
                        break;
                }
            }

            List<FilePreviewItem> fileItems = new List<FilePreviewItem>(files.Values);
            foreach (var fileItem in fileItems) {
                fileItem.Items.Sort(LocationComparer);
            }

            fileItems.Sort(FileComparer);
            return fileItems;
        }

        /// <summary>
        /// Gets the original name of the variable/member being renamed.
        /// </summary>
        public string OriginalName { get; }

        /// <summary>
        /// Gets the private prefix class name minus the leading underscore.
        /// </summary>
        public string PrivatePrefix { get; }

        public RenameVariableRequest Request { get; }

        private static int FileComparer(FilePreviewItem left, FilePreviewItem right) {
            return String.Compare(left.Filename, right.Filename, StringComparison.OrdinalIgnoreCase);
        }

        private static int LocationComparer(IPreviewItem leftItem, IPreviewItem rightItem) {
            var left = (LocationPreviewItem)leftItem;
            var right = (LocationPreviewItem)rightItem;

            if (left.Line != right.Line) {
                return left.Line - right.Line;
            }

            return left.Column - right.Column;
        }

        public int ApplyChanges() {
            _input.ClearRefactorPane();
            _input.OutputLog(Strings.RefactorPreviewChangesRenaming.FormatUI(OriginalName, Request.Name));

            var undo = _input.BeginGlobalUndo();
            try {
                foreach (FilePreviewItem changedFile in _list.Items) {
                    var buffer = _input.GetBufferForDocument(changedFile.Filename);

                    changedFile.UpdateBuffer(buffer);
                }
            } finally {
                _input.EndGlobalUndo(undo);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Gets the text of the OK button
        /// </summary>
        public int GetConfirmation(out string pbstrConfirmation) {
            pbstrConfirmation = Strings.RefactorPreviewChangesRenamingConfirmationButton;
            return VSConstants.S_OK;
        }

        public int GetDescription(out string pbstrDescription) {
            pbstrDescription = Strings.RefactorPreviewChangesRenamingDescription.FormatUI(_expr, Request.Name);
            return VSConstants.S_OK;
        }

        public int GetHelpContext(out string pbstrHelpContext) {
            throw new NotImplementedException();
        }

        public int GetRootChangesList(out object ppIUnknownPreviewChangesList) {
            ppIUnknownPreviewChangesList = _list;
            return VSConstants.S_OK;
        }

        public int GetTextViewDescription(out string pbstrTextViewDescription) {
            pbstrTextViewDescription = Strings.RefactorPreviewChangesTextViewDescription;
            return VSConstants.S_OK;
        }

        public int GetTitle(out string pbstrTitle) {
            pbstrTitle = Strings.RefactorPreviewChangesRenameVariableTitle;
            return VSConstants.S_OK;
        }

        public int GetWarning(out string pbstrWarning, out int ppcwlWarningLevel) {
            pbstrWarning = null;
            ppcwlWarningLevel = 0;
            return VSConstants.S_OK;
        }
    }
}
