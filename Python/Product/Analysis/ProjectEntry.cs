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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
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
    internal sealed class ProjectEntry : IPythonProjectEntry {
        private readonly PythonAnalyzer _projectState;
        private readonly string _moduleName;
        private readonly string _filePath;
        private readonly ModuleInfo _myScope;
        private IAnalysisCookie _cookie;
        private PythonAst _tree;
        private ModuleAnalysis _currentAnalysis;
        private AnalysisUnit _unit;
        private int _analysisVersion;
        private Dictionary<object, object> _properties = new Dictionary<object, object>();
        private ManualResetEventSlim _curWaiter;
        private int _updatesPending, _waiters;

        // we expect to have at most 1 waiter on updated project entries, so we attempt to share the event.
        private static ManualResetEventSlim _sharedWaitEvent = new ManualResetEventSlim(false);

        internal ProjectEntry(PythonAnalyzer state, string moduleName, string filePath, IAnalysisCookie cookie) {
            _projectState = state;
            _moduleName = moduleName ?? "";
            _filePath = filePath;
            _cookie = cookie;
            _myScope = new ModuleInfo(_moduleName, this, state.Interpreter.CreateModuleContext());
            _unit = new AnalysisUnit(_tree, _myScope.Scope);
            AnalysisLog.NewUnit(_unit);
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
                    _tree = null;
                    return;
                }

                _tree = newAst;
                _cookie = newCookie;

                if (_curWaiter != null) {
                    _curWaiter.Set();
                }
            }

            var newParse = OnNewParseTree;
            if (newParse != null) {
                newParse(this, EventArgs.Empty);
            }
        }

        public void GetTreeAndCookie(out PythonAst tree, out IAnalysisCookie cookie) {
            lock (this) {
                tree = _tree;
                cookie = _cookie;
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

            return gotNewTree ? _tree : null;
        }

        public void Analyze(CancellationToken cancel) {
            Analyze(cancel, false);
        }

        public void Analyze(CancellationToken cancel, bool enqueueOnly) {
            if (cancel.IsCancellationRequested) {
                return;
            }
            lock (this) {
                _analysisVersion++;

                Parse(enqueueOnly, cancel);
            }

            if (!enqueueOnly) {
                RaiseOnNewAnalysis();
            }
        }

        internal void RaiseOnNewAnalysis() {
            var evt = OnNewAnalysis;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public int AnalysisVersion {
            get {
                return _analysisVersion;
            }
        }

        public bool IsAnalyzed {
            get {
                return Analysis != null;
            }
        }

        private void Parse(bool enqueueOnly, CancellationToken cancel) {
            PythonAst tree;
            IAnalysisCookie cookie;
            GetTreeAndCookie(out tree, out cookie);
            if (tree == null) {
                return;
            }

            var oldParent = _myScope.ParentPackage;
            if (_filePath != null) {
                ProjectState.ModulesByFilename[_filePath] = _myScope;
            }

            if (oldParent != null) {
                // update us in our parent package
                oldParent.AddChildPackage(_myScope, _unit);
            } else if (_filePath != null) {
                // we need to check and see if our parent is a package for the case where we're adding a new
                // file but not re-analyzing our parent package.
                string parentFilename;
                if (ModulePath.IsInitPyFile(_filePath)) {
                    // subpackage
                    parentFilename = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_filePath)), "__init__.py");
                } else {
                    // just a module
                    parentFilename = Path.Combine(Path.GetDirectoryName(_filePath), "__init__.py");
                }

                ModuleInfo parentPackage;
                if (ProjectState.ModulesByFilename.TryGetValue(parentFilename, out parentPackage)) {
                    parentPackage.AddChildPackage(_myScope, _unit);
                }
            }

            _unit = new AnalysisUnit(tree, _myScope.Scope);
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
            _myScope.Specialize();

            // It may be that we have analyzed some child packages of this package already, but because it wasn't analyzed,
            // the children were not registered. To handle this possibility, scan analyzed packages for children of this
            // package (checked by module name first, then sanity-checked by path), and register any that match.
            if (ModulePath.IsInitPyFile(_filePath)) {
                string pathPrefix = Path.GetDirectoryName(_filePath) + "\\";
                var children =
                    from pair in _projectState.ModulesByFilename
                    // Is the candidate child package in a subdirectory of our package?
                    let fileName = pair.Key
                    where fileName.StartsWith(pathPrefix) 
                    let moduleName = pair.Value.Name
                    // Is the full name of the candidate child package qualified with the name of our package?
                    let lastDot = moduleName.LastIndexOf('.')
                    where lastDot > 0
                    let parentModuleName = moduleName.Substring(0, lastDot)
                    where parentModuleName == _myScope.Name
                    select pair.Value;
                foreach (var child in children) {
                    _myScope.AddChildPackage(child, _unit);
                }
            }

            _unit.Enqueue();
            
            if (!enqueueOnly) {
                _projectState.AnalyzeQueuedEntries(cancel);
            }

            // publish the analysis now that it's complete/running
            _currentAnalysis = new ModuleAnalysis(
                _unit, 
                ((ModuleScope)_unit.Scope).CloneForPublish(),
                cookie);
        }

        public IGroupableAnalysisProject AnalysisGroup {
            get {
                return _projectState;
            }
        }

        public string GetLine(int lineNo) {
            return _cookie.GetLine(lineNo);
        }

        public ModuleAnalysis Analysis {
            get { return _currentAnalysis; }
        }

        public string FilePath {
            get { return _filePath; }
        }

        public IAnalysisCookie Cookie {
            get { return _cookie; }
        }

        internal PythonAnalyzer ProjectState {
            get { return _projectState; }
        }

        public PythonAst Tree {
            get { return _tree; }
        }

        internal ModuleInfo MyScope {
            get { return _myScope; }
        }

        public IModuleContext AnalysisContext {
            get { return _myScope.InterpreterContext; }
        }

        public string ModuleName {
            get {
                return _moduleName;
            }
        }

        public Dictionary<object, object> Properties {
            get {
                if (_properties == null) {
                    _properties = new Dictionary<object, object>();
                }
                return _properties;
            }
        }


        #region IProjectEntry2 Members

        public void RemovedFromProject() {
            _analysisVersion = -1;
        }

        #endregion

        internal void Enqueue() {
            _unit.Enqueue();
        }
    }

    /// <summary>
    /// Represents a unit of work which can be analyzed.
    /// </summary>
    public interface IAnalyzable {
        void Analyze(CancellationToken cancel);
    }

    /// <summary>
    /// Represents a file which is capable of being analyzed.  Can be cast to other project entry types
    /// for more functionality.  See also IPythonProjectEntry and IXamlProjectEntry.
    /// </summary>
    public interface IProjectEntry : IAnalyzable {
        /// <summary>
        /// Returns true if the project entry has been parsed and analyzed.
        /// </summary>
        bool IsAnalyzed { get; }

        /// <summary>
        /// Returns the current analysis version of the project entry.
        /// </summary>
        int AnalysisVersion {
            get;
        }

        /// <summary>
        /// Returns the project entries file path.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Gets the specified line of text from the project entry.
        /// </summary>
        string GetLine(int lineNo);

        /// <summary>
        /// Provides storage of arbitrary properties associated with the project entry.
        /// </summary>
        Dictionary<object, object> Properties {
            get;
        }

        /// <summary>
        /// Called when the project entry is removed from the project.
        /// 
        /// Implementors of this method must ensure this method is thread safe.
        /// </summary>
        void RemovedFromProject();


        IModuleContext AnalysisContext {
            get;
        }
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

        IGroupableAnalysisProject AnalysisGroup {
            get;
        }
    }

    /// <summary>
    /// Represents a project which can support more efficent analysis of individual items via
    /// analyzing them together.
    /// </summary>
    public interface IGroupableAnalysisProject {
        void AnalyzeQueuedEntries(CancellationToken cancel);
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
