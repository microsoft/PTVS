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

using System.IO;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;
using TestUtilities.Mocks;

namespace TestUtilities.Python {
    internal sealed class EditorTestToolset {
        private readonly ExportProvider _exportProvider;
        private readonly MockServiceProvider _serviceProvider;
        public UIThreadBase UIThread { get; }

        public EditorTestToolset(bool useRealUIThread = true) {
            _exportProvider = MefExportProviders.CreateEditorExportProvider();

            var settingsManager = new MockSettingsManager();
            settingsManager.Store.AllowEmptyCollections = true;

            _serviceProvider = _exportProvider.GetExportedValue<MockServiceProvider>();
            _serviceProvider.Services[typeof(SVsSettingsManager).GUID] = settingsManager;

            if (useRealUIThread) {
                _serviceProvider.AddService(typeof(UIThreadBase), new UIThread(new JoinableTaskFactory(_exportProvider.GetExportedValue<JoinableTaskContext>())));
            } else {
                _serviceProvider.AddService(typeof(UIThreadBase), new MockUIThread());
            }

            UIThread = (UIThreadBase)_serviceProvider.GetService(typeof(UIThreadBase));
        }

        public T GetService<T>() => (T)_serviceProvider.GetService(typeof(T));

        public EditorTestToolset WithPythonToolsService() {
            _serviceProvider.AddService(typeof(IPythonToolsOptionsService), new MockPythonToolsOptionsService());
            _serviceProvider.AddService(typeof(PythonToolsService), new PythonToolsService(_serviceProvider));
            return this;
        }

        public ITextBuffer CreatePythonTextBuffer(string input, VsProjectAnalyzer testAnalyzer = null) {
            var filePath = Path.Combine(TestData.GetTempPath(), Path.GetRandomFileName(), "file.py");
            return CreatePythonTextBuffer(input, filePath, testAnalyzer);
        }

        public ITextBuffer CreatePythonTextBuffer(string input, string filePath, VsProjectAnalyzer testAnalyzer = null) {
            var textBufferFactory = _exportProvider.GetExportedValue<ITextBufferFactoryService>();
            var textDocumentFactoryService = _exportProvider.GetExportedValue<ITextDocumentFactoryService>();
            var textContentType = _exportProvider.GetExportedValue<IContentTypeRegistryService>().GetContentType(PythonCoreConstants.ContentType);

            var textBuffer = textBufferFactory.CreateTextBuffer(input, textContentType);
            textDocumentFactoryService.CreateTextDocument(textBuffer, filePath);

            if (testAnalyzer != null) {
                textBuffer.Properties.AddProperty(VsProjectAnalyzer._testAnalyzer, testAnalyzer);
            }

            textBuffer.Properties.AddProperty(VsProjectAnalyzer._testFilename, filePath);

            return textBuffer;
        }

        public PythonEditorServices GetPythonEditorServices()
            => _exportProvider.GetExportedValue<PythonEditorServices>();

        public IWpfTextView CreatePythonTextView(string input)
            => CreateTextView(CreatePythonTextBuffer(input));

        public IWpfTextView CreateTextView(ITextBuffer textBuffer)
            => UIThread.Invoke(() => CreateTextView_MainThread(textBuffer));

        private IWpfTextView CreateTextView_MainThread(ITextBuffer textBuffer) {
            var textEditorFactory = _exportProvider.GetExportedValue<ITextEditorFactoryService>();
            var roleSet = textEditorFactory.CreateTextViewRoleSet(PredefinedTextViewRoles.Analyzable,
                PredefinedTextViewRoles.Document,
                PredefinedTextViewRoles.Editable,
                PredefinedTextViewRoles.Interactive,
                PredefinedTextViewRoles.PrimaryDocument,
                PredefinedTextViewRoles.Structured,
                PredefinedTextViewRoles.Zoomable,
                PredefinedTextViewRoles.Debuggable);
            return textEditorFactory.CreateTextView(textBuffer, roleSet, CreateOptions());
        }

        private IEditorOptions CreateOptions() {
            var editorOptionsFactory = _exportProvider.GetExportedValue<IEditorOptionsFactoryService>();
            var options = editorOptionsFactory.CreateOptions();

            options.SetOptionValue("IsCodeLensEnabled", false);

            options.SetOptionValue(DefaultTextViewOptions.UseVisibleWhitespaceId, true);
            options.SetOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionId, true);

            options.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginId, true);
            options.SetOptionValue(DefaultTextViewHostOptions.OutliningMarginId, true);

            options.SetOptionValue(DefaultTextViewHostOptions.HorizontalScrollBarId, true);
            options.SetOptionValue(DefaultTextViewHostOptions.VerticalScrollBarId, true);

            return options;
        }
    }
}