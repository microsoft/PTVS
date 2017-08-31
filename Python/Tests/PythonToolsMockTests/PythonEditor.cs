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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.MockVsTests;
using TestUtilities;

namespace PythonToolsMockTests {
    public sealed class PythonEditor : IDisposable {
        private readonly bool _disposeVS, _disposeFactory, _disposeAnalyzer;
        public readonly MockVs VS;
        public readonly IPythonInterpreterFactory Factory;
        public readonly VsProjectAnalyzer Analyzer;
        public readonly MockVsTextView View;
        public readonly AdvancedEditorOptions AdvancedOptions;

        public PythonEditor(
            string content = null,
            PythonLanguageVersion version = PythonLanguageVersion.V27,
            MockVs vs = null,
            IPythonInterpreterFactory factory = null,
            VsProjectAnalyzer analyzer = null,
            string filename = null
        ) {
            if (vs == null) {
                _disposeVS = true;
                vs = new MockVs();
            }
            MockVsTextView view = null;
            try {
                AdvancedEditorOptions advancedOptions = null;
                vs.InvokeSync(() => {
                    advancedOptions = vs.GetPyService().AdvancedOptions;
                    advancedOptions.AutoListMembers = true;
                    advancedOptions.AutoListIdentifiers = false;
                });
                AdvancedOptions = advancedOptions;

                if (factory == null) {
                    _disposeFactory = true;
                    factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(version.ToVersion());
                }
                if (analyzer == null) {
                    _disposeAnalyzer = true;
                    vs.InvokeSync(() => {
                        analyzer = new VsProjectAnalyzer(vs.ComponentModel.GetService<PythonEditorServices>(), factory, outOfProcAnalyzer: false);
                    });
                    var task = analyzer.ReloadTask;
                    if (task != null) {
                        task.WaitAndUnwrapExceptions();
                    }
                }
                if (string.IsNullOrEmpty(filename)) {
                    do {
                        filename = PathUtils.GetAbsoluteFilePath(TestData.GetTempPath(), Path.GetRandomFileName()) + ".py";
                    } while (File.Exists(filename));
                }

                var cancel = CancellationTokens.After60s;
                using (var mre = new ManualResetEventSlim()) {
                    view = vs.CreateTextView(PythonCoreConstants.ContentType, content ?? "",
                        v => {
                            v.TextView.TextBuffer.Properties[BufferParser.ParseImmediately] = true;
                            v.TextView.TextBuffer.Properties[IntellisenseController.SuppressErrorLists] = IntellisenseController.SuppressErrorLists;
                            v.TextView.TextBuffer.Properties[VsProjectAnalyzer._testAnalyzer] = analyzer;
                            v.TextView.TextBuffer.Properties[VsProjectAnalyzer._testFilename] = filename;
                        },
                        filename);

                    var entry = analyzer.GetAnalysisEntryFromPath(filename);
                    while (entry == null && !cancel.IsCancellationRequested) {
                        Thread.Sleep(50);
                        entry = analyzer.GetAnalysisEntryFromPath(filename);
                    }

                    if (!string.IsNullOrEmpty(content) && !cancel.IsCancellationRequested && !entry.IsAnalyzed) {
                        EventHandler evt = (s, e) => mre.SetIfNotDisposed();

                        try {
                            entry.AnalysisComplete += evt;
                            while (!mre.Wait(50, cancel) && !vs.HasPendingException) { }
                        } catch (OperationCanceledException) {
                        } finally {
                            analyzer.AnalysisStarted -= evt;
                        }
                    }
                    if (cancel.IsCancellationRequested) {
                        Assert.Fail("Timed out waiting for code analysis");
                    }

                    vs.ThrowPendingException();
                }

                View = view;
                view = null;
                Analyzer = analyzer;
                analyzer = null;
                Factory = factory;
                factory = null;
                VS = vs;
                vs = null;
            } finally {
                if (view != null) {
                    view.Dispose();
                }
                if (analyzer != null && _disposeAnalyzer) {
                    analyzer.Dispose();
                }
                if (factory != null && _disposeFactory) {
                    var disp = factory as IDisposable;
                    if (disp != null) {
                        disp.Dispose();
                    }
                }
                if (vs != null && _disposeVS) {
                    vs.Dispose();
                }
            }
        }

        public string Text {
            get { return View.Text; }
            set {
                var buffer = View.TextView.TextBuffer;
                var bi = PythonTextBufferInfo.TryGetForBuffer(buffer);

                if (bi?.AnalysisEntry == null) {
                    // No analysis yet, so just set the text.
                    using (var edit = View.TextView.TextBuffer.CreateEdit()) {
                        edit.Delete(0, edit.Snapshot.Length);
                        edit.Insert(0, value);
                        edit.Apply();
                    }
                    return;
                }

                using (var edit = buffer.CreateEdit()) {
                    edit.Delete(0, edit.Snapshot.Length);
                    edit.Apply();
                }

                if (string.IsNullOrEmpty(value)) {
                    return;
                }

                using (ManualResetEventSlim mre1 = new ManualResetEventSlim(), mre2 = new ManualResetEventSlim()) {
                    EventHandler evt1 = (s, e) => mre1.SetIfNotDisposed();
                    EventHandler evt2 = (s, e) => mre2.SetIfNotDisposed();
                    Analyzer.AnalysisStarted += evt1;
                    bi.AnalysisEntry.AnalysisComplete += evt2;

                    try {
                        using (var edit = View.TextView.TextBuffer.CreateEdit()) {
                            edit.Insert(0, value);
                            edit.Apply();
                        }

                        if (!mre1.Wait(0)) {
                            if (!mre1.Wait(10000)) {
                                throw new TimeoutException("Failed to see buffer start analyzer");
                            }
                            mre2.Reset();
                        }

                        if (!mre2.Wait(10000)) {
                            throw new TimeoutException("Failed to see entry finish analyzing");
                        }
                    } finally {
                        Analyzer.AnalysisStarted -= evt1;
                        bi.AnalysisEntry.AnalysisComplete -= evt2;
                    }
                }
            }
        }

        public ITextSnapshot CurrentSnapshot {
            get { return View.TextView.TextSnapshot; }
        }

        public WaitHandle AnalysisCompleteEvent {
            get {
                var evt = new AnalysisCompleteManualResetEvent(BufferInfo);
                BufferInfo.AddSink(evt, evt);
                return evt;
            }
        }

        class AnalysisCompleteManualResetEvent : WaitHandle, IPythonTextBufferInfoEventSink {
            private readonly ManualResetEvent _event;
            private readonly PythonTextBufferInfo _info;

            public AnalysisCompleteManualResetEvent(PythonTextBufferInfo info) {
                _event = new ManualResetEvent(false);
                _info = info;
                SafeWaitHandle = _event.SafeWaitHandle;
            }

            protected override void Dispose(bool explicitDisposing) {
                SafeWaitHandle = null;
                base.Dispose(explicitDisposing);
                _event.Dispose();
                if (explicitDisposing) {
                    _info.RemoveSink(this);
                }
            }

            public Task PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
                if (e.Event == PythonTextBufferInfoEvents.NewAnalysis) {
                    if (_info.LastAnalysisReceivedVersion.VersionNumber >= _info.Buffer.CurrentSnapshot.Version.VersionNumber) {
                        try {
                            _event.Set();
                        } catch (ObjectDisposedException) {
                        }
                        _info.RemoveSink(this);
                    }
                }
                return Task.CompletedTask;
            }
        }

        internal PythonEditorServices EditorServices => VS.ComponentModel.GetService<PythonEditorServices>();

        internal PythonTextBufferInfo BufferInfo => EditorServices.GetBufferInfo(View.TextView.TextBuffer);

        public List<Completion> GetCompletionListAfter(string substring, bool assertIfNoCompletions = true) {
            var snapshot = CurrentSnapshot;
            return GetCompletionList(snapshot.GetText().IndexOfEnd(substring), assertIfNoCompletions, snapshot);
        }

        public List<Completion> GetCompletionList(
            int index,
            bool assertIfNoCompletions = true,
            ITextSnapshot snapshot = null
        ) {
            snapshot = snapshot ?? CurrentSnapshot;
            if (index < 0) {
                index += snapshot.Length + 1;
            }
            View.MoveCaret(new SnapshotPoint(snapshot, index));
            VS.Invoke(() => View.MemberList());
            using (var sh = View.WaitForSession<ICompletionSession>(assertIfNoSession: assertIfNoCompletions)) {
                if (sh == null) {
                    return new List<Completion>();
                }
                return sh.Session.CompletionSets.SelectMany(cs => cs.Completions).ToList();
            }
        }

        public void Backspace() => VS.InvokeSync(() => View.Backspace());
        public void Enter() => VS.InvokeSync(() => View.Enter());
        public void Clear() => VS.InvokeSync(() => View.Clear());
        public void MoveCaret(int line, int column) => View.MoveCaret(line, column);
        public void MemberList() => VS.InvokeSync(() => View.MemberList());
        public void ParamInfo() => VS.InvokeSync(() => View.ParamInfo());
        public void Type(string text) => VS.InvokeSync(() => View.Type(text));

        public void TypeAndWaitForAnalysis(string text) {
            using (var mre = new ManualResetEventSlim()) {
                EventHandler evt = (s, e) => mre.SetIfNotDisposed();
                Analyzer.AnalysisStarted += evt;

                Type(text);

                var cts = CancellationTokens.After60s;
                try {
                    while (!mre.Wait(500, cts) && !VS.HasPendingException) { }
                    Analyzer.WaitForCompleteAnalysis(x => !cts.IsCancellationRequested && !VS.HasPendingException);
                } catch (OperationCanceledException) {
                } finally {
                    Analyzer.AnalysisStarted -= evt;
                }
                if (cts.IsCancellationRequested) {
                    Assert.Fail("Timed out waiting for code analysis");
                }
                VS.ThrowPendingException();
            }
        }

        public IEnumerable<string> GetCompletions(int index) {
            return GetCompletionList(index, false).Select(c => c.DisplayText);
        }

        public IEnumerable<string> GetCompletionsAfter(string substring) {
            return GetCompletionListAfter(substring, false).Select(c => c.DisplayText);
        }

        public object GetAnalysisEntry(ITextBuffer buffer = null) {
            var entryService = VS.ComponentModel.GetService<AnalysisEntryService>();
            AnalysisEntry entry;
            if (buffer == null) {
                entryService.TryGetAnalysisEntry(View.TextView, out entry);
            } else {
                entryService.TryGetAnalysisEntry(buffer, out entry);
            }
            return entry ?? throw new ArgumentException("no AnalysisEntry available");
        }

        public void Dispose() {
            if (View != null) {
                View.Dispose();
            }
            if (Analyzer != null && _disposeAnalyzer) {
                Analyzer.Dispose();
            }
            if (Factory != null && _disposeFactory) {
                var disp = Factory as IDisposable;
                if (disp != null) {
                    disp.Dispose();
                }
            }
            if (VS != null && _disposeVS) {
                VS.Dispose();
            }
        }
    }
}
