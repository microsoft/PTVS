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
        private int _version;
        private Dictionary<object, object> _properties = new Dictionary<object, object>();

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
                _tree = newAst;
                _cookie = newCookie;
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

        public void Analyze() {
            Analyze(false);
        }

        public void Analyze(bool enqueueOnly) {
            lock (this) {
                _version++;

                Parse(enqueueOnly);
            }

            var newAnalysis = OnNewAnalysis;
            if (newAnalysis != null) {
                newAnalysis(this, EventArgs.Empty);
            }
        }

        public int Version {
            get {
                return _version;
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
                oldParent.AddChildPackage(_myScope, _unit, _moduleName.Substring(_moduleName.IndexOf('.') + 1));
            }

            var unit = _unit = new AnalysisUnit(_tree, new InterpreterScope[] { _myScope.Scope });

            _scopeTree = new Stack<InterpreterScope>();
            _scopeTree.Push(MyScope.Scope);

            // create new analysis object and add to the queue to be analyzed
            var newAnalysis = new ModuleAnalysis(_unit, _scopeTree);
            _unit.Enqueue();
            
            // collect top-level definitions first
            var walker = new OverviewWalker(this, unit);
            _tree.Walk(walker);

            PublishPackageChildrenInPackage();

            if (!enqueOnly) {
                ((IGroupableAnalysisProject)_projectState).AnalyzeQueuedEntries();
            }

            // publish the analysis now that it's complete
            _currentAnalysis = newAnalysis;

            foreach (var variableInfo in _myScope.Scope.Variables) {
                variableInfo.Value.ClearOldValues(this);
            }
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
                            _myScope.AddChildPackage(childModule, _unit, Path.GetFileNameWithoutExtension(file));
                        }
                    }

                    foreach (var packageDir in Directory.GetDirectories(dir)) {
                        string package = Path.Combine(packageDir, "__init__.py");
                        ModuleInfo childPackage;
                        if (File.Exists(package) && _projectState.ModulesByFilename.TryGetValue(package, out childPackage)) {
                            _myScope.AddChildPackage(childPackage, _unit, Path.GetFileName(packageDir));
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

    public interface IAnalyzable {
        void Analyze();
    }
    
    /// <summary>
    /// Represents a file which is capable of being analyzed.  Can be cast to other project entry types
    /// for more functionality.  See also IPythonProjectEntry and IXamlProjectEntry.
    /// </summary>
    public interface IProjectEntry : IAnalyzable {
        bool IsAnalyzed { get; }
        int Version {
            get;
        }

        string FilePath { get; }
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
        PythonAst Tree {
            get;
        }

        ModuleAnalysis Analysis {
            get;
        }

        event EventHandler<EventArgs> OnNewParseTree;
        event EventHandler<EventArgs> OnNewAnalysis;

        void UpdateTree(PythonAst ast, IAnalysisCookie fileCookie);
        void GetTreeAndCookie(out PythonAst ast, out IAnalysisCookie cookie);

    }
}
