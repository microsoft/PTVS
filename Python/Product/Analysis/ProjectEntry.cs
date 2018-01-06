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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides interactions to analysis a single file in a project and get the results back.
    /// 
    /// To analyze a file the tree should be updated with a call to UpdateTree and then PreParse
    /// should be called on all files.  Finally Parse should then be called on all files.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "Unclear ownership makes it unlikely this object will be disposed correctly")]
    internal sealed class ProjectEntry : IPythonProjectEntry, IAggregateableProjectEntry, IDocument {
        private AnalysisUnit _unit;
        private ManualResetEventSlim _curWaiter;
        private int _updatesPending, _waiters;
        private readonly DocumentBuffer _buffer;
        internal readonly HashSet<AggregateProjectEntry> _aggregates = new HashSet<AggregateProjectEntry>();

        // we expect to have at most 1 waiter on updated project entries, so we attempt to share the event.
        private static ManualResetEventSlim _sharedWaitEvent = new ManualResetEventSlim(false);

        internal ProjectEntry(
            PythonAnalyzer state,
            string moduleName,
            string filePath,
            Uri documentUri,
            IAnalysisCookie cookie
        ) {
            ProjectState = state;
            ModuleName = moduleName ?? "";
            DocumentUri = documentUri ?? MakeDocumentUri(filePath);
            FilePath = filePath;
            Cookie = cookie;
            MyScope = new ModuleInfo(ModuleName, this, state.Interpreter.CreateModuleContext());
            _unit = new AnalysisUnit(Tree, MyScope.Scope);
            _buffer = new DocumentBuffer();
            if (Cookie is InitialContentCookie c) {
                _buffer.Reset(c.Version, c.Content);
            }
            AnalysisLog.NewUnit(_unit);
        }

        internal static Uri MakeDocumentUri(string filePath) {
            Uri u;
            if (!Path.IsPathRooted(filePath)) {
                u = new Uri($"file:///LOCAL-PATH/{filePath.Replace('\\', '/')}");
            } else {
                u = new Uri(filePath);
            }

            return u;
        }

        public event EventHandler<EventArgs> OnNewParseTree;
        public event EventHandler<EventArgs> OnNewAnalysis;

        public void UpdateTree(PythonAst newAst, IAnalysisCookie newCookie) {
            lock (this) {
                if (_updatesPending > 0) {
                    _updatesPending--;
                }
                if (newAst == null) {
                    // there was an error in parsing, just let the waiter go...
                    if (_curWaiter != null) {
                        _curWaiter.Set();
                    }
                    Tree = null;
                    return;
                }

                Tree = newAst;
                Cookie = newCookie;

                if (_curWaiter != null) {
                    _curWaiter.Set();
                }
            }

            OnNewParseTree?.Invoke(this, EventArgs.Empty);
        }

        internal bool IsVisible(ProjectEntry assigningScope) {
            return true;
        }

        public void GetTreeAndCookie(out PythonAst tree, out IAnalysisCookie cookie) {
            lock (this) {
                tree = Tree;
                cookie = Cookie;
            }
        }

        public void BeginParsingTree() {
            lock (this) {
                _updatesPending++;
            }
        }

        public PythonAst WaitForCurrentTree(int timeout = -1) {
            lock (this) {
                if (_updatesPending == 0) {
                    return Tree;
                }

                _waiters++;
                if (_curWaiter == null) {
                    _curWaiter = Interlocked.Exchange(ref _sharedWaitEvent, null);
                    if (_curWaiter == null) {
                        _curWaiter = new ManualResetEventSlim(false);
                    } else {
                        _curWaiter.Reset();
                    }
                }
            }

            bool gotNewTree = _curWaiter.Wait(timeout);

            lock (this) {
                _waiters--;
                if (_waiters == 0 &&
                    Interlocked.CompareExchange(ref _sharedWaitEvent, _curWaiter, null) != null) {
                    _curWaiter.Dispose();
                }
                _curWaiter = null;
            }

            return gotNewTree ? Tree : null;
        }

        public void Analyze(CancellationToken cancel) {
            Analyze(cancel, false);
        }

        public void Analyze(CancellationToken cancel, bool enqueueOnly) {
            if (cancel.IsCancellationRequested) {
                return;
            }

            lock (this) {
                AnalysisVersion++;

                foreach (var aggregate in _aggregates) {
                    aggregate.BumpVersion();
                }

                Parse(enqueueOnly, cancel);
            }

            if (!enqueueOnly) {
                RaiseOnNewAnalysis();
            }
        }

        internal void RaiseOnNewAnalysis() {
            OnNewAnalysis?.Invoke(this, EventArgs.Empty);
        }

        public int AnalysisVersion { get; private set; }

        public bool IsAnalyzed => Analysis != null;

        private void Parse(bool enqueueOnly, CancellationToken cancel) {
            PythonAst tree;
            IAnalysisCookie cookie;
            GetTreeAndCookie(out tree, out cookie);
            if (tree == null) {
                return;
            }

            var oldParent = MyScope.ParentPackage;
            if (FilePath != null) {
                ProjectState.ModulesByFilename[FilePath] = MyScope;
            }

            if (oldParent != null) {
                // update us in our parent package
                oldParent.AddChildPackage(MyScope, _unit);
            } else if (FilePath != null) {
                // we need to check and see if our parent is a package for the case where we're adding a new
                // file but not re-analyzing our parent package.
                string parentFilename;
                if (ModulePath.IsInitPyFile(FilePath)) {
                    // subpackage
                    parentFilename = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(FilePath)), "__init__.py");
                } else {
                    // just a module
                    parentFilename = Path.Combine(Path.GetDirectoryName(FilePath), "__init__.py");
                }

                ModuleInfo parentPackage;
                if (ProjectState.ModulesByFilename.TryGetValue(parentFilename, out parentPackage)) {
                    parentPackage.AddChildPackage(MyScope, _unit);
                }
            }

            _unit = new AnalysisUnit(tree, MyScope.Scope);
            AnalysisLog.NewUnit(_unit);

            foreach (var value in MyScope.Scope.AllVariables) {
                value.Value.EnqueueDependents();
            }

            MyScope.Scope.Children.Clear();
            MyScope.Scope.ClearNodeScopes();
            MyScope.Scope.ClearNodeValues();
            MyScope.ClearUnresolvedModules();

            // collect top-level definitions first
            var walker = new OverviewWalker(this, _unit, tree);
            tree.Walk(walker);

            MyScope.Specialize();

            // It may be that we have analyzed some child packages of this package already, but because it wasn't analyzed,
            // the children were not registered. To handle this possibility, scan analyzed packages for children of this
            // package (checked by module name first, then sanity-checked by path), and register any that match.
            if (ModulePath.IsInitPyFile(FilePath)) {
                string pathPrefix = Path.GetDirectoryName(FilePath) + "\\";
                var children =
                    from pair in ProjectState.ModulesByFilename
                    // Is the candidate child package in a subdirectory of our package?
                    let fileName = pair.Key
                    where fileName.StartsWith(pathPrefix)
                    let moduleName = pair.Value.Name
                    // Is the full name of the candidate child package qualified with the name of our package?
                    let lastDot = moduleName.LastIndexOf('.')
                    where lastDot > 0
                    let parentModuleName = moduleName.Substring(0, lastDot)
                    where parentModuleName == MyScope.Name
                    select pair.Value;
                foreach (var child in children) {
                    MyScope.AddChildPackage(child, _unit);
                }
            }

            _unit.Enqueue();

            if (!enqueueOnly) {
                ProjectState.AnalyzeQueuedEntries(cancel);
            }

            // publish the analysis now that it's complete/running
            Analysis = new ModuleAnalysis(
                _unit,
                ((ModuleScope)_unit.Scope).CloneForPublish()
            );
        }

        private void ClearScope() {
            MyScope.Scope.Children.Clear();
            MyScope.Scope.ClearNodeScopes();
            MyScope.Scope.ClearNodeValues();
            MyScope.ClearUnresolvedModules();
        }

        public IGroupableAnalysisProject AnalysisGroup => ProjectState;

        public ModuleAnalysis Analysis { get; private set; }

        public string FilePath { get; }

        public IAnalysisCookie Cookie { get; private set; }

        internal PythonAnalyzer ProjectState { get; private set; }

        public PythonAst Tree { get; private set; }

        internal ModuleInfo MyScope { get; private set; }

        public IModuleContext AnalysisContext => MyScope.InterpreterContext;

        public string ModuleName { get; }

        public Uri DocumentUri { get; }

        public Dictionary<object, object> Properties { get; } = new Dictionary<object, object>();


        public void RemovedFromProject() {
            AnalysisVersion = -1;
            foreach (var aggregatedInto in _aggregates) {
                if (aggregatedInto.AnalysisVersion != -1) {
                    ProjectState.ClearAggregate(aggregatedInto);
                    aggregatedInto.RemovedFromProject();
                }
            }
        }

        internal void Enqueue() {
            _unit.Enqueue();
        }

        public void AggregatedInto(AggregateProjectEntry into) {
            _aggregates.Add(into);
        }

        public Stream ReadDocumentBytes(out int version) {
            lock (_buffer) {
                if (_buffer.Version >= 0) {
                    version = _buffer.Version;
                    // TODO: Remove chunkSize argument
                    return EncodeToStream(_buffer.Text, Encoding.UTF8, 16);
                } else {
                    var s = PathUtils.OpenWithRetry(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    version = s == null ? -1 : 0;
                    return s;
                }
            }
        }

        internal static Stream EncodeToStream(StringBuilder text, Encoding encoding, int chunkSize = 4096) {
            if (text.Length < chunkSize) {
                return new MemoryStream(encoding.GetBytes(text.ToString()));
            }

            var ms = new MemoryStream(encoding == Encoding.Unicode ? text.Length * 2 : text.Length);
            var enc = encoding.GetEncoder();
            var chars = new char[chunkSize];
            var bytes = new byte[encoding.GetMaxByteCount(chunkSize)];
            for (int i = 0; i < text.Length;) {
                bool flush = true;
                int len = text.Length - i;
                if (len > chars.Length) {
                    flush = false;
                    len = chars.Length;
                }
                text.CopyTo(i, chars, 0, len);
                enc.Convert(chars, 0, len, bytes, 0, bytes.Length, flush, out int charCount, out int bytesUsed, out _);
                i += charCount;
                ms.Write(bytes, 0, bytesUsed);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public TextReader ReadDocument(out int version) {
            var stream = ReadDocumentBytes(out version);
            if (stream == null) {
                version = -1;
                return null;
            }
            try {
                var sr = Parser.ReadStreamWithEncoding(stream, ProjectState.LanguageVersion);
                stream = null;
                version = 0;
                return sr;
            } finally {
                stream?.Dispose();
            }
        }

        public int DocumentVersion {
            get {
                lock (_buffer) {
                    return _buffer.Version;
                }
            }
        }

        public void UpdateDocument(DocumentChangeSet changes) {
            lock (_buffer) {
                _buffer.Update(changes);
            }
        }

        public void ResetDocument(int version, string content) {
            lock (_buffer) {
                _buffer.Reset(version, content);
            }
        }
    }

    class InitialContentCookie : IAnalysisCookie {
        public string Content { get; set; }
        public int Version { get; set; }
    }

    /// <summary>
    /// Represents a unit of work which can be analyzed.
    /// </summary>
    public interface IAnalyzable {
        void Analyze(CancellationToken cancel);
    }

    public interface IVersioned {
        /// <summary>
        /// Returns the current analysis version of the project entry.
        /// </summary>
        int AnalysisVersion { get; }
    }

    /// <summary>
    /// Represents a file which is capable of being analyzed.  Can be cast to other project entry types
    /// for more functionality.  See also IPythonProjectEntry and IXamlProjectEntry.
    /// </summary>
    public interface IProjectEntry : IAnalyzable, IVersioned {
        /// <summary>
        /// Returns true if the project entry has been parsed and analyzed.
        /// </summary>
        bool IsAnalyzed { get; }

        
        /// <summary>
        /// Returns the project entries file path.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Returns the document unique identifier
        /// </summary>
        Uri DocumentUri { get; }

        /// <summary>
        /// Provides storage of arbitrary properties associated with the project entry.
        /// </summary>
        Dictionary<object, object> Properties { get; }

        /// <summary>
        /// Called when the project entry is removed from the project.
        /// 
        /// Implementors of this method must ensure this method is thread safe.
        /// </summary>
        void RemovedFromProject();

        IModuleContext AnalysisContext { get; }
    }

    /// <summary>
    /// Represents a project entry which is created by an interpreter for additional
    /// files which it supports analyzing.  Provides the ParseContent method which
    /// is called when the parse queue is ready to update the file contents.
    /// </summary>
    public interface IExternalProjectEntry : IProjectEntry {
        void ParseContent(TextReader content, IAnalysisCookie fileCookie);
    }

    /// <summary>
    /// Represents a project entry which can be analyzed together with other project entries for
    /// more efficient analysis.
    /// 
    /// To analyze the full group you call Analyze(true) on all the items in the same group (determined
    /// by looking at the identity of the AnalysGroup object).  Then you call AnalyzeQueuedEntries on the
    /// group.
    /// </summary>
    public interface IGroupableAnalysisProjectEntry {
        /// <summary>
        /// Analyzes this project entry optionally just adding it to the queue shared by the project.
        /// </summary>
        void Analyze(CancellationToken cancel, bool enqueueOnly);

        IGroupableAnalysisProject AnalysisGroup { get; }
    }

    /// <summary>
    /// Represents a project which can support more efficent analysis of individual items via
    /// analyzing them together.
    /// </summary>
    public interface IGroupableAnalysisProject {
        void AnalyzeQueuedEntries(CancellationToken cancel);
    }

    public interface IDocument {
        TextReader ReadDocument(out int version);
        Stream ReadDocumentBytes(out int version);

        int DocumentVersion { get; }

        void UpdateDocument(DocumentChangeSet changes);
        void ResetDocument(int version, string content);
    }

    public interface IPythonProjectEntry : IGroupableAnalysisProjectEntry, IProjectEntry {
        /// <summary>
        /// Returns the last parsed AST.
        /// </summary>
        PythonAst Tree {
            get;
        }

        string ModuleName { get; }

        ModuleAnalysis Analysis {
            get;
        }

        event EventHandler<EventArgs> OnNewParseTree;
        event EventHandler<EventArgs> OnNewAnalysis;

        /// <summary>
        /// Informs the project entry that a new tree will soon be available and will be provided by
        /// a call to UpdateTree.  Calling this method will cause WaitForCurrentTree to block until
        /// UpdateTree has been called.
        /// 
        /// Calls to BeginParsingTree should be balanced with calls to UpdateTree.
        /// 
        /// This method is thread safe.
        /// </summary>
        void BeginParsingTree();

        void UpdateTree(PythonAst ast, IAnalysisCookie fileCookie);
        void GetTreeAndCookie(out PythonAst ast, out IAnalysisCookie cookie);

        /// <summary>
        /// Returns the current tree if no parsing is currently pending, otherwise waits for the 
        /// current parse to finish and returns the up-to-date tree.
        /// </summary>
        PythonAst WaitForCurrentTree(int timeout = -1);
    }
}
