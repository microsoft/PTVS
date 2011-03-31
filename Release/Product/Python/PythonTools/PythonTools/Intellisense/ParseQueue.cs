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
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides an asynchronous queue for parsing source code.  Multiple items
    /// may be parsed simultaneously.  Text buffers are monitored for changes and
    /// the parser is called when the buffer should be re-parsed.
    /// </summary>
    internal class ParseQueue {
        internal readonly ProjectAnalyzer _parser;
        private int _analysisPending;

        /// <summary>
        /// Creates a new parse queue which will parse using the provided parser.
        /// </summary>
        /// <param name="parser"></param>
        public ParseQueue(ProjectAnalyzer parser) {
            _parser = parser;
        }

        /// <summary>
        /// Parses the specified text buffer.  Continues to monitor the parsed buffer and updates
        /// the parse tree asynchronously as the buffer changes.
        /// </summary>
        /// <param name="buffer"></param>
        public BufferParser EnqueueBuffer(IProjectEntry projEntry, ITextBuffer buffer) {
            // only attach one parser to each buffer, we can get multiple enqueue's
            // for example if a document is already open when loading a project.
            BufferParser bufferParser;
            if (!buffer.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out bufferParser)) {
                bufferParser = new BufferParser(projEntry, _parser, new[] { buffer });
                
                buffer.Properties.AddProperty(typeof(BufferParser), bufferParser);
                
                var curSnapshot = buffer.CurrentSnapshot;
                var severity = PythonToolsPackage.Instance != null ? PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity : Severity.Ignore;
                EnqueWorker(() => {
                    _parser.ParseBuffers(bufferParser, severity, curSnapshot);
                });
            }
            
            return bufferParser;
        }

        public void UnEnqueueBuffer(ITextBuffer buffer) {
            BufferParser parser;
            if (buffer.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out parser)) {
                buffer.ChangedLowPriority -= parser.BufferChangedLowPriority;
                buffer.Properties.RemoveProperty(typeof(BufferParser));                
            }
        }

        /// <summary>
        /// Parses the specified file on disk.
        /// </summary>
        /// <param name="filename"></param>
        public void EnqueueFile(IProjectEntry projEntry, string filename) {
            var severity = PythonToolsPackage.Instance != null ? PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity : Severity.Ignore;
            EnqueWorker(() => {
                for (int i = 0; i < 10; i++) {
                    try {
                        using (var reader = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                            _parser.ParseFile(projEntry, filename, reader, severity);
                            return;
                        }
                    } catch (IOException) {
                        // file being copied, try again...
                        Thread.Sleep(100);
                    }
                } 
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

        internal static TextReader GetContentProvider(ITextBuffer buffer, ITextSnapshot snapshot) {
            return new SnapshotSpanSourceCodeReader(new SnapshotSpan(snapshot, new Span(0, snapshot.Length)));
        }

        public bool IsParsing {
            get {
                return _analysisPending > 0;
            }
        }
    }

    class BufferParser {
        internal ProjectAnalyzer _parser;
        private readonly Timer _timer;
        private IList<ITextBuffer> _buffers;
        private bool _parsing, _requeue, _textChange;
        internal IProjectEntry _currentProjEntry;
        private Severity _indentationInconsistencySeverity;

        private const int ReparseDelay = 1000;      // delay in MS before we re-parse a buffer w/ non-line changes.

        public BufferParser(IProjectEntry initialProjectEntry, ProjectAnalyzer parser, IList<ITextBuffer> buffers) {
            _parser = parser;
            _timer = new Timer(ReparseWorker, null, Timeout.Infinite, Timeout.Infinite);
            _buffers = buffers;
            _currentProjEntry = initialProjectEntry;
            if (PythonToolsPackage.Instance != null) {
                _indentationInconsistencySeverity = PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity;
                PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencyChanged += OptionsPage_IndentationInconsistencyChanged;
            }
            foreach (var buffer in buffers) {
                InitBuffer(buffer);
            }
        }

        void OptionsPage_IndentationInconsistencyChanged(object sender, EventArgs e) {
            _indentationInconsistencySeverity = PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencySeverity;
        }

        public void StopMonitoring() {
            foreach (var buffer in _buffers) {
                buffer.ChangedLowPriority -= BufferChangedLowPriority;
                buffer.Properties.RemoveProperty(typeof(BufferParser));
            }
            PythonToolsPackage.Instance.OptionsPage.IndentationInconsistencyChanged -= OptionsPage_IndentationInconsistencyChanged;
        }

        public ITextBuffer[] Buffers {
            get {
                return _buffers.ToArray();
            }
        }

        internal void AddBuffer(ITextBuffer textBuffer) {
            lock (this) {
                EnsureMutableBuffers();
 
                _buffers.Add(textBuffer);

                InitBuffer(textBuffer);
            }
        }

        internal void RemoveBuffer(ITextBuffer subjectBuffer) {
            lock (this) {
                EnsureMutableBuffers();

                UninitBuffer(subjectBuffer);

                _buffers.Remove(subjectBuffer);
            }
        }

        private void UninitBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.Properties.RemoveProperty(typeof(IProjectEntry));
            subjectBuffer.ChangedLowPriority -= BufferChangedLowPriority;
        }

        private void InitBuffer(ITextBuffer buffer) {
            buffer.ChangedLowPriority += BufferChangedLowPriority;
            buffer.Properties.AddProperty(typeof(IProjectEntry), _currentProjEntry);
        }

        private void EnsureMutableBuffers() {
            if (_buffers.IsReadOnly) {
                _buffers = new List<ITextBuffer>(_buffers);
            }
        }

        internal void ReparseWorker(object unused) {
            ITextSnapshot[] snapshots;
            lock (this) {
                if (_parsing) {
                    return;
                }

                _parsing = true;
                snapshots = new ITextSnapshot[_buffers.Count];
                for (int i = 0; i < _buffers.Count; i++) {
                    snapshots[i] = _buffers[i].CurrentSnapshot;
                }
            }

            _parser.ParseBuffers(this, _indentationInconsistencySeverity, snapshots);

            lock (this) {
                _parsing = false;
                if (_requeue) {
                    ThreadPool.QueueUserWorkItem(ReparseWorker);
                }
                _requeue = false;
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
            ThreadPool.QueueUserWorkItem(ReparseWorker);
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
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
                if (change.OldText != "" || change.NewText != Environment.NewLine) {
                    mixedChanges = true;
                    break;
                }
            }
            return mixedChanges;
        }

    }
}
