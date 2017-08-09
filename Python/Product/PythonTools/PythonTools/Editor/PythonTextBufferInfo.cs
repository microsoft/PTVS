using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Editor {
    sealed class PythonTextBufferInfo : IDisposable {
        public static PythonTextBufferInfo ForBuffer(PythonEditorServices services, ITextBuffer buffer) {
            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(PythonTextBufferInfo),
                () => new PythonTextBufferInfo(services, buffer)
            );
        }

        public static PythonTextBufferInfo TryGetForBuffer(ITextBuffer buffer) {
            PythonTextBufferInfo bi;
            if (buffer == null) {
                return null;
            }
            return buffer.Properties.TryGetProperty(typeof(PythonTextBufferInfo), out bi) ? bi : null;
        }

        public static bool TryDispose(ITextBuffer buffer) {
            PythonTextBufferInfo bi;
            if (buffer.Properties.TryGetProperty(typeof(PythonTextBufferInfo), out bi)) {
                buffer.Properties.RemoveProperty(typeof(PythonTextBufferInfo));
                bi?.Dispose();
                return true;
            }
            return false;
        }

        private readonly object _lock = new object();
        private bool _isDisposed;
        private readonly Lazy<string> _filename;
        private int _analysisEntryId;
        private AnalysisEntry _analysisEntry;
        private PythonClassifier _classifier;
        private PythonAnalysisClassifier _analysisClassifier;
        private OutliningTaggerProvider.OutliningTagger _outliningTagger;

        private PythonTextBufferInfo(PythonEditorServices services, ITextBuffer buffer) {
            Services = services;
            Buffer = buffer;
            _filename = new Lazy<string>(GetOrCreateFilename);
            _analysisEntryId = -1;
            Buffer.ContentTypeChanged += Buffer_ContentTypeChanged;
            Buffer.Changed += Buffer_Changed;
            Buffer.ChangedLowPriority += Buffer_ChangedLowPriority;
        }

        public void Dispose() {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;
            Buffer.ContentTypeChanged -= Buffer_ContentTypeChanged;
            Buffer.Changed -= Buffer_Changed;
            Buffer.ChangedLowPriority -= Buffer_ChangedLowPriority;
            TrySetAnalysisEntry(null, AnalysisEntry);
        }

        private T GetOrCreate<T>(ref T destination, Func<PythonTextBufferInfo, T> creator) where T : class {
            if (destination != null) {
                return destination;
            }
            var created = creator(this);
            lock (_lock) {
                if (destination == null) {
                    destination = created;
                } else {
                    created = destination;
                }
            }
            return created;
        }

        private string GetOrCreateFilename() {
            var replEval = Buffer.GetInteractiveWindow()?.GetPythonEvaluator();
            if (replEval != null) {
                return replEval.AnalysisFilename;
            }

            ITextDocument doc;
            if (Buffer.Properties.TryGetProperty(typeof(ITextDocument), out doc)) {
                return doc.FilePath;
            }

            return "{0}.py".FormatInvariant(Guid.NewGuid());
        }



        public ITextBuffer Buffer { get; }
        public ITextSnapshot CurrentSnapshot => Buffer.CurrentSnapshot;
        public IContentType ContentType => Buffer.ContentType;
        public string Filename => _filename.Value;

        public PythonEditorServices Services { get; }

        public PythonLanguageVersion LanguageVersion => AnalysisEntry?.Analyzer.LanguageVersion ?? PythonLanguageVersion.None;

        #region Events

        private event EventHandler _onNewAnalysisEntry;
        public event EventHandler OnNewAnalysis;
        public event EventHandler OnNewAnalysisEntry {
            add { lock (_lock) _onNewAnalysisEntry += value; }
            remove { lock (_lock) _onNewAnalysisEntry -= value; }
        }
        public event EventHandler OnNewParseTree;

        public event EventHandler<TextContentChangedEventArgs> OnChanged;
        private void Buffer_Changed(object sender, TextContentChangedEventArgs e) {
            if (!_isDisposed) {
                OnChanged?.Invoke(this, e);
            }
        }

        public event EventHandler<TextContentChangedEventArgs> OnChangedLowPriority;
        private void Buffer_ChangedLowPriority(object sender, TextContentChangedEventArgs e) {
            if (!_isDisposed) {
                OnChangedLowPriority?.Invoke(this, e);
            }
        }


        public event EventHandler<ContentTypeChangedEventArgs> OnContentTypeChanged;
        private void Buffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            if (!_isDisposed) {
                OnContentTypeChanged?.Invoke(this, e);
            }
        }

        #endregion

        #region IntelliSense-related hooks

        public PythonClassifier Classifier => _classifier;
        public PythonClassifier GetOrCreateClassifier(Func<PythonTextBufferInfo, PythonClassifier> creator) => GetOrCreate(ref _classifier, creator);

        public PythonAnalysisClassifier AnalysisClassifier => _analysisClassifier;
        public PythonAnalysisClassifier GetOrCreateAnalysisClassifier(Func<PythonTextBufferInfo, PythonAnalysisClassifier> creator) => GetOrCreate(ref _analysisClassifier, creator);

        public OutliningTaggerProvider.OutliningTagger OutliningTagger => _outliningTagger;
        public OutliningTaggerProvider.OutliningTagger GetOrCreateOutliningTagger(Func<PythonTextBufferInfo, OutliningTaggerProvider.OutliningTagger> creator) => GetOrCreate(ref _outliningTagger, creator);


        #endregion

        #region Analysis Info

        public AnalysisEntry AnalysisEntry => Volatile.Read(ref _analysisEntry);
        public bool TrySetAnalysisEntry(AnalysisEntry entry, AnalysisEntry ifCurrent) {
            if (entry == ifCurrent) {
                return true;
            }
            if (_isDisposed && entry != null) {
                throw new ObjectDisposedException(GetType().Name);
            }

            var previous = Interlocked.CompareExchange(ref _analysisEntry, entry, ifCurrent);
            if (previous != ifCurrent) {
                return false;
            }

            if (previous != null) {
                previous.AnalysisComplete -= AnalysisEntry_AnalysisComplete;
                previous.ParseComplete -= AnalysisEntry_ParseComplete;
                previous.Analyzer.BufferDetached(previous, Buffer);
            }
            if (entry != null) {
                entry.AnalysisComplete += AnalysisEntry_AnalysisComplete;
                entry.ParseComplete += AnalysisEntry_ParseComplete;
            }

            if (!_isDisposed) {
                _onNewAnalysisEntry?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }

        private void AnalysisEntry_ParseComplete(object sender, EventArgs e) {
            OnNewParseTree?.Invoke(this, e);
        }

        private void AnalysisEntry_AnalysisComplete(object sender, EventArgs e) {
            OnNewAnalysis?.Invoke(this, e);
        }

        public int AnalysisEntryId => Volatile.Read(ref _analysisEntryId);
        public bool SetAnalysisEntryId(int id) {
            if (id < 0) {
                Volatile.Write(ref _analysisEntryId, -1);
                return true;
            }
            return Interlocked.CompareExchange(ref _analysisEntryId, id, -1) == -1;
        }

        public ITextSnapshot LastSentSnapshot { get; set; }
        public ITextVersion LastParseReceivedVersion { get; private set; }
        public ITextVersion LastAnalysisReceivedVersion { get; private set; }

        public bool UpdateLastReceivedParse(int version) {
            var ver = LastAnalysisReceivedVersion ?? Buffer.CurrentSnapshot.Version;
            if (ver == null || ver.VersionNumber >= version) {
                return false;
            }

            while (ver != null && ver.VersionNumber < version) {
                ver = ver.Next;
            }
            LastParseReceivedVersion = ver;
            return true;
        }

        public bool UpdateLastReceivedAnalysis(int version) {
            var ver = LastAnalysisReceivedVersion ?? Buffer.CurrentSnapshot.Version;
            if (ver == null || ver.VersionNumber >= version) {
                return false;
            }

            while (ver != null && ver.VersionNumber < version) {
                ver = ver.Next;
            }
            LastAnalysisReceivedVersion = ver;
            return true;
        }

        public Task<AnalysisEntry> WaitForAnalysisEntryAsync(CancellationToken cancellationToken) {
            // Return our current entry if we have one
            var entry = AnalysisEntry;
            if (entry != null) {
                return Task.FromResult(entry);
            }

            var tcs = new TaskCompletionSource<AnalysisEntry>();
            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => tcs.TrySetCanceled());
            }

            EventHandler handler = null;
            handler = (s, e) => {
                cancellationToken.ThrowIfCancellationRequested();
                var bi = (PythonTextBufferInfo)s;
                var result = bi.AnalysisEntry;
                if (result != null) {
                    bi.OnNewAnalysisEntry -= handler;
                    tcs.TrySetResult(result);
                }
            };

            return tcs.Task;
        }


        #endregion
    }
}
