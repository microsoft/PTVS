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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    using ProjectFileEntry = ProjectFileInfo;

    /// <summary>
    /// Provides an asynchronous queue for parsing source code.  Multiple items
    /// may be parsed simultaneously.  Text buffers are monitored for changes and
    /// the parser is called when the buffer should be re-parsed.
    /// </summary>
    internal class ParseQueue {
        internal readonly VsProjectAnalyzer _parser;
        internal int _analysisPending;

        /// <summary>
        /// Creates a new parse queue which will parse using the provided parser.
        /// </summary>
        /// <param name="parser"></param>
        public ParseQueue(VsProjectAnalyzer parser) {
            _parser = parser;
        }
#if FALSE
        /// <summary>
        /// Parses the specified text buffer.  Continues to monitor the parsed buffer and updates
        /// the parse tree asynchronously as the buffer changes.
        /// </summary>
        /// <param name="buffer"></param>
        public BufferParser EnqueueBuffer(ProjectFileEntry projEntry, ITextView textView, ITextBuffer buffer) {
            // only attach one parser to each buffer, we can get multiple enqueue's
            // for example if a document is already open when loading a project.
            BufferParser bufferParser;
            if (!buffer.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out bufferParser)) {
                Dispatcher dispatcher = null;
                var uiElement = textView as UIElement;
                if (uiElement != null) {
                    dispatcher = uiElement.Dispatcher;
                }
                bufferParser = new BufferParser(this, dispatcher, projEntry, _parser, buffer);

                var curSnapshot = buffer.CurrentSnapshot;
                var severity = _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
                bufferParser.EnqueingEntry();
                EnqueWorker(() => {
                    _parser.ParseBuffers(bufferParser, severity, curSnapshot);
                });
            } else {
                bufferParser.AttachedViews++;
            }
            
            return bufferParser;
        }

        /// <summary>
        /// Parses the specified file on disk.
        /// </summary>
        /// <param name="filename"></param>
        public void EnqueueFile(ProjectFileEntry projEntry, string filename) {
            var severity = _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
            EnqueWorker(() => {
                for (int i = 0; i < 10; i++) {
                    try {
                        if (!File.Exists(filename)) {
                            break;
                        }
                        using (var reader = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                            _parser.ParseFile(projEntry, filename, reader, severity);
                            return;
                        }
                    } catch (IOException) {
                        // file being copied, try again...
                        Thread.Sleep(100);
                    } catch (UnauthorizedAccessException) {
                        // file is inaccessible, try again...
                        Thread.Sleep(100);
                    }
                }
                // failed to parse, keep the UpdateTree calls balanced
                projEntry.UpdateTree(null, null);
            });
        }
#endif
        public void EnqueueZipArchiveEntry(ProjectFileEntry projEntry, string zipFileName, ZipArchiveEntry entry, Action onComplete) {
            var pathInArchive = entry.FullName.Replace('/', '\\');
            var fileName = Path.Combine(zipFileName, pathInArchive);
            var severity = _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
            EnqueWorker(() => {
                try {
                    using (var stream = entry.Open()) {
                        //_parser.ParseFile(projEntry, fileName, stream, severity);
                        return;
                    }
                } catch (IOException ex) {
                    Debug.Fail(ex.Message);
                } catch (InvalidDataException ex) {
                    Debug.Fail(ex.Message);
                } finally {
                    onComplete();
                }
#if FALSE
                IPythonProjectEntry pyEntry = projEntry as IPythonProjectEntry;
                if (pyEntry != null) {
                    // failed to parse, keep the UpdateTree calls balanced
                    pyEntry.UpdateTree(null, null);
                }
#endif
            });
        }

        private void EnqueWorker(Action parser) {
            Interlocked.Increment(ref _analysisPending);

            ThreadPool.QueueUserWorkItem(
                dummy => {
                    try {
                        parser();
                    } finally {
                        Interlocked.Decrement(ref _analysisPending);
                    }
                }
            );
        }

        public bool IsParsing {
            get {
                return _analysisPending > 0;
            }
        }

        public int ParsePending {
            get {
                return _analysisPending;
            }
        }
    }

    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "ownership is unclear")]
    class BufferParser {
        internal VsProjectAnalyzer _parser;
        private readonly Timer _timer;
        private readonly Dispatcher _dispatcher;
        private IList<ITextBuffer> _buffers;
        private bool _parsing, _requeue, _textChange;
        internal ProjectFileEntry _currentProjEntry;
        private ITextDocument _document;
        public int AttachedViews;

        private const int ReparseDelay = 1000;      // delay in MS before we re-parse a buffer w/ non-line changes.

        public BufferParser(Dispatcher dispatcher, ProjectFileEntry initialProjectEntry, VsProjectAnalyzer parser, ITextBuffer buffer) {
            _parser = parser;
            _timer = new Timer(ReparseTimer, null, Timeout.Infinite, Timeout.Infinite);
            _buffers = new[] { buffer };
            _currentProjEntry = initialProjectEntry;
            _dispatcher = dispatcher;
            AttachedViews = 1;

            InitBuffer(buffer);
        }

        private Severity IndentationInconsistencySeverity {
            get {
                return _parser.PyService.GeneralOptions.IndentationInconsistencySeverity;
            }
        }

        public void StopMonitoring() {
            foreach (var buffer in _buffers) {
                buffer.ChangedLowPriority -= BufferChangedLowPriority;
                buffer.Properties.RemoveProperty(typeof(BufferParser));
                if (_document != null) {
                    _document.EncodingChanged -= EncodingChanged;
                    _document = null;
                }
            }
            _timer.Dispose();
        }

        public Dispatcher Dispatcher {
            get {
                return _dispatcher;
            }
        }

        public ITextBuffer[] Buffers {
            get {
                return _buffers.Where(
                    x => !x.Properties.ContainsProperty(PythonReplEvaluator.InputBeforeReset)
                ).ToArray();
            }
        }

        internal void AddBuffer(ITextBuffer textBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                _buffers.Add(textBuffer);

                InitBuffer(textBuffer);

                _parser.ConnectErrorList(_currentProjEntry, textBuffer);
            }
        }

        internal void RemoveBuffer(ITextBuffer subjectBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                UninitBuffer(subjectBuffer);

                _buffers.Remove(subjectBuffer);

                _parser.DisconnectErrorList(_currentProjEntry, subjectBuffer);
            }
        }

        private void UninitBuffer(ITextBuffer subjectBuffer) {
            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
            subjectBuffer.Properties.RemoveProperty(typeof(ProjectFileEntry));
            subjectBuffer.Properties.RemoveProperty(typeof(BufferParser));
            subjectBuffer.ChangedLowPriority -= BufferChangedLowPriority;
        }

        private void InitBuffer(ITextBuffer buffer) {
            buffer.Properties.AddProperty(typeof(BufferParser), this);
            buffer.ChangedLowPriority += BufferChangedLowPriority;
            buffer.Properties.AddProperty(typeof(ProjectFileEntry), _currentProjEntry);
            
            if (_document != null) {
                _document.EncodingChanged -= EncodingChanged;
                _document = null;
            }
            if (buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out _document) && _document != null) {
                _document.EncodingChanged += EncodingChanged;
            }
        }

        private void EnsureMutableBuffers() {
            if (_buffers.IsReadOnly) {
                _buffers = new List<ITextBuffer>(_buffers);
            }
        }

        internal void ReparseTimer(object unused) {
            RequeueWorker();
        }

        internal void ReparseWorker(object unused) {
            ITextSnapshot[] snapshots;
            lock (this) {
                if (_parsing) {
                    NotReparsing();
                    Interlocked.Decrement(ref _parser._analysisPending);
                    return;
                }

                _parsing = true;
                var buffers = Buffers;
                snapshots = new ITextSnapshot[buffers.Length];
                for (int i = 0; i < buffers.Length; i++) {
                    snapshots[i] = buffers[i].CurrentSnapshot;
                }
            }

            _parser.ParseBuffers(this, IndentationInconsistencySeverity, snapshots);
            Interlocked.Decrement(ref _parser._analysisPending);

            lock (this) {
                _parsing = false;
                if (_requeue) {
                    RequeueWorker();
                }
                _requeue = false;
            }
        }

        /// <summary>
        /// Called when we decide we need to re-parse a buffer but before we start the buffer.
        /// </summary>
        internal void EnqueingEntry() {
            lock (this) {
                _currentProjEntry.BeginParsingTree();
            }
        }

        /// <summary>
        /// Called when we race and are not actually re-parsing a buffer, balances the calls
        /// of BeginParsingTree when we aren't parsing.
        /// </summary>
        private void NotReparsing() {
            lock (this) {
                _currentProjEntry.UpdateTree(null, null);
                /*
                IPythonProjectEntry pyEntry = _currentProjEntry as IPythonProjectEntry;
                if (pyEntry != null) {
                    pyEntry.UpdateTree(null, null);
                }*/
            }
        }

        internal void EncodingChanged(object sender, EncodingChangedEventArgs e) {
            lock (this) {
                if (_parsing) {
                    // we are currently parsing, just reque when we complete
                    _requeue = true;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                } else {
                    Requeue();
                }
            }
        }

        internal void BufferChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            lock (this) {
                // only immediately re-parse on line changes after we've seen a text change.

                if (_parsing) {
                    // we are currently parsing, just reque when we complete
                    _requeue = true;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                } else if (LineAndTextChanges(e)) {
                    // user pressed enter, we should reque immediately
                    Requeue();
                } else {
                    // parse if the user doesn't do anything for a while.
                    _textChange = IncludesTextChanges(e);
                    _timer.Change(ReparseDelay, Timeout.Infinite);
                }
            }
        }

        internal void Requeue() {
            RequeueWorker();
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void RequeueWorker() {
            Interlocked.Increment(ref _parser._analysisPending);
            EnqueingEntry();
            ThreadPool.QueueUserWorkItem(ReparseWorker);
        }

        /// <summary>
        /// Used to track if we have line + text changes, just text changes, or just line changes.
        /// 
        /// If we have text changes followed by a line change we want to immediately reparse.
        /// If we have just text changes we want to reparse in ReparseDelay ms from the last change.
        /// If we have just repeated line changes (e.g. someone's holding down enter) we don't want to
        ///     repeatedly reparse, instead we want to wait ReparseDelay ms.
        /// </summary>
        private bool LineAndTextChanges(TextContentChangedEventArgs e) {
            if (_textChange) {
                _textChange = false;
                return e.Changes.IncludesLineChanges;
            }

            bool mixedChanges = false;
            if (e.Changes.IncludesLineChanges) {
                mixedChanges = IncludesTextChanges(e);
            }

            return mixedChanges;
        }

        /// <summary>
        /// Returns true if the change incldues text changes (not just line changes).
        /// </summary>
        private static bool IncludesTextChanges(TextContentChangedEventArgs e) {
            bool mixedChanges = false;
            foreach (var change in e.Changes) {
                if (!string.IsNullOrEmpty(change.OldText) || change.NewText != Environment.NewLine) {
                    mixedChanges = true;
                    break;
                }
            }
            return mixedChanges;
        }


        internal ITextDocument Document {
            get {
                return _document;
            }
        }
    }
}
