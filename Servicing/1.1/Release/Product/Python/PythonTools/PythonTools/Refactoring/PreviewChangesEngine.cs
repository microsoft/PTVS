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
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Implements our preview changes engine.  Creates a list of all of the preview items based upon the analyzed expression
    /// and rename variable request.
    /// </summary>
    class PreviewChangesEngine : IVsPreviewChangesEngine {
        private readonly ExpressionAnalysis _analysis;
        private readonly RenameVariableRequest _renameReq;
        private readonly PreviewList _list;
        internal readonly IRenameVariableInput _input;
        internal readonly ProjectAnalyzer _analayzer;
        private readonly string _originalName, _privatePrefix;
        private readonly IEnumerable<IAnalysisVariable> _variables;

        public PreviewChangesEngine(IRenameVariableInput input, ExpressionAnalysis analysis, RenameVariableRequest request, string originalName, string privatePrefix, ProjectAnalyzer analyzer, IEnumerable<IAnalysisVariable> variables) {
            _analysis = analysis;
            _analayzer = analyzer;
            _renameReq = request;
            _originalName = originalName;
            _privatePrefix = privatePrefix;
            _variables = variables;
            _input = input;
            _list = new PreviewList(CreatePreviewItems().ToArray());
        }

        private List<FilePreviewItem> CreatePreviewItems() {
            Dictionary<string, FilePreviewItem> files = new Dictionary<string, FilePreviewItem>();
            Dictionary<FilePreviewItem, HashSet<LocationInfo>> allItems = new Dictionary<FilePreviewItem, HashSet<LocationInfo>>();

            foreach (var variable in _variables) {
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
                            fileItem.Items.Add(new LocationPreviewItem(fileItem, variable.Location, variable.Type));
                            curLocations.Add(variable.Location);
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
        public string OriginalName {
            get {
                return _originalName;
            }
        }

        /// <summary>
        /// Gets the private prefix class name minus the leading underscore.
        /// </summary>
        public string PrivatePrefix {
            get {
                return _privatePrefix;
            }
        }

        public RenameVariableRequest Request {
            get {
                return _renameReq;
            }
        }

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
            _input.OutputLog(String.Format("Renaming '{0}' to '{1}'", _originalName, _renameReq.Name));

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
            pbstrConfirmation = "Apply";
            return VSConstants.S_OK;
        }

        public int GetDescription(out string pbstrDescription) {
            pbstrDescription = String.Format("&Rename '{0}' to '{1}'", _analysis.Expression, _renameReq.Name);
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
            pbstrTextViewDescription = "&Preview Code Changes:";
            return VSConstants.S_OK;
        }

        public int GetTitle(out string pbstrTitle) {
            pbstrTitle = "Rename variable";
            return VSConstants.S_OK;
        }

        public int GetWarning(out string pbstrWarning, out int ppcwlWarningLevel) {
            pbstrWarning = null;
            ppcwlWarningLevel = 0;
            return VSConstants.S_OK;
        }
    }
}
