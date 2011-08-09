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
using System.Threading;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides interactions to analysis a single file in a project and get the results back.
    /// 
    /// To analyze a file the tree should be updated with a call to UpdateTree and then PreParse
    /// should be called on all files.  Finally Parse should then be called on all files.
    /// </summary>
    internal sealed class ProjectEntry : IPythonProjectEntry {
        private readonly PythonAnalyzer _projectState;
        private readonly string _moduleName;
        private readonly string _filePath;
        private IAnalysisCookie _cookie;
        private ModuleInfo _myScope;
        private PythonAst _tree;
        private Stack<InterpreterScope> _scopeTree;
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
            _unit = new AnalysisUnit(_tree, new InterpreterScope[] { _myScope.Scope });
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

        public void Analyze() {
            Analyze(false);
        }

        public void Analyze(bool enqueueOnly) {
            lock (this) {
                _analysisVersion++;

                Parse(enqueueOnly);
            }

            var newAnalysis = OnNewAnalysis;
            if (newAnalysis != null) {
                newAnalysis(this, EventArgs.Empty);
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

        private void Parse(bool enqueOnly) {
            if (_tree == null) {
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
                if (Path.GetFileName(_filePath).Equals("__init__.py", StringComparison.OrdinalIgnoreCase)) {
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

            var unit = _unit = new AnalysisUnit(_tree, new InterpreterScope[] { _myScope.Scope });

            _scopeTree = new Stack<InterpreterScope>();
            _scopeTree.Push(MyScope.Scope);
            MyScope.Scope.Children.Clear();

            // create new analysis object and add to the queue to be analyzed
            var newAnalysis = new ModuleAnalysis(_unit, _scopeTree);
            _unit.Enqueue();
            
            // collect top-level definitions first
            var walker = new OverviewWalker(this, unit);
            _tree.Walk(walker);

            PublishPackageChildrenInPackage();

            if (!enqueOnly) {
                ((IGroupableAnalysisProject)_projectState).AnalyzeQueuedEntries();


                List<string> toRemove = null;
                foreach (var variableInfo in _myScope.Scope.Variables) {
                    variableInfo.Value.ClearOldValues(this);
                    if (variableInfo.Value._dependencies.Count == 0 &&
                        variableInfo.Value.Types.Count == 0) {
                        if (toRemove == null) {
                            toRemove = new List<string>();
                        }
                        toRemove.Add(variableInfo.Key);
                    }
                }
                if (toRemove != null) {
                    foreach (var name in toRemove) {
                        _myScope.Scope.Variables.Remove(name);
                    }
                }
            }

            // publish the analysis now that it's complete
            _currentAnalysis = newAnalysis;

        }

        public IGroupableAnalysisProject AnalysisGroup {
            get {
                return _projectState;
            }
        }

        private void PublishPackageChildrenInPackage() {
            if (_filePath != null && _filePath.EndsWith("__init__.py")) {
                string dir = Path.GetDirectoryName(_filePath);
                if (Directory.Exists(dir)) {
                    foreach (var file in Directory.GetFiles(dir)) {
                        if (file.EndsWith("__init__.py")) {
                            continue;
                        }

                        ModuleInfo childModule;
                        if (_projectState.ModulesByFilename.TryGetValue(file, out childModule)) {
                            _myScope.AddChildPackage(childModule, _unit);
                        }
                    }

                    foreach (var packageDir in Directory.GetDirectories(dir)) {
                        string package = Path.Combine(packageDir, "__init__.py");
                        ModuleInfo childPackage;
                        if (File.Exists(package) && _projectState.ModulesByFilename.TryGetValue(package, out childPackage)) {
                            _myScope.AddChildPackage(childPackage, _unit);
                        }
                    }
                }
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

    }

    /// <summary>
    /// Represents a unit of work which can be analyzed.
    /// </summary>
    public interface IAnalyzable {
        void Analyze();
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
        void Analyze(bool enqueueOnly);

        IGroupableAnalysisProject AnalysisGroup {
            get;
        }
    }

    /// <summary>
    /// Represents a project which can support more efficent analysis of individual items via
    /// analyzing them together.
    /// </summary>
    public interface IGroupableAnalysisProject {
        void AnalyzeQueuedEntries();
    }

    public interface IPythonProjectEntry : IGroupableAnalysisProjectEntry, IProjectEntry {
        /// <summary>
        /// Returns the last parsed AST.
        /// </summary>
        PythonAst Tree {
            get;
        }

        ModuleAnalysis Analysis {
            get;
        }

        event EventHandler<EventArgs> OnNewParseTree;
        event EventHandler<EventArgs> OnNewAnalysis;

        /// <summary>
        /// Informs thhe project entry that a new tree will soon be available and will be provided by
        /// a call to UpdateTree.  Calling this method will cause WaitForCurrentTree to block until
        /// UpdateTree has been called.
        /// 
        /// Calls to BeginParsingTree should be balanced with calls to UpdateTree.
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
