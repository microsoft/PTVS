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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.Windows.Design.Host;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    public class PythonProjectNode : CommonProjectNode, IPythonProject, IPythonProject2 {
        // For files that are analyzed because they were directly or indirectly referenced in the search path, store the information
        // about the directory from the search path that referenced them in IProjectEntry.Properties[_searchPathEntryKey], so that
        // they can be located and removed when that directory is removed from the path.
        private static readonly object _searchPathEntryKey = new { Name = "SearchPathEntry" };

        private DesignerContext _designerContext;
        private IPythonInterpreter _interpreter;
        private VsProjectAnalyzer _analyzer;
        private readonly HashSet<string> _errorFiles = new HashSet<string>();
        private bool _defaultInterpreter;
        private PythonDebugPropertyPage _debugPropPage;
        private CommonSearchPathContainerNode _searchPathContainer;

        public PythonProjectNode(CommonProjectPackage package)
            : base(package, Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList))) {

            Type projectNodePropsType = typeof(PythonProjectNodeProperties);
            AddCATIDMapping(projectNodePropsType, projectNodePropsType.GUID);
        }

        private static string GetSearchPathEntry(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_searchPathEntryKey, out result);
            return (string)result;
        }

        private static void SetSearchPathEntry(IProjectEntry entry, string value) {
            entry.Properties[_searchPathEntryKey] = value;
        }

        public override CommonFileNode CreateCodeFileNode(MsBuildProjectElement item) {
            return new PythonFileNode(this, item);
        }

        public override CommonFileNode CreateNonCodeFileNode(MsBuildProjectElement item) {
            return new PythonNonCodeFileNode(this, item);
        }

        protected override ReferenceContainerNode CreateReferenceContainerNode() {
            return new PythonReferenceContainerNode(this);
        }

        protected override void LinkFileAdded(string filename) {
            if (PythonToolsPackage.Instance.OptionsPage.UpdateSearchPathsWhenAddingLinkedFiles) {
                // update our search paths.
                string dirToAdd = Path.GetDirectoryName(filename);
                while (!String.IsNullOrEmpty(dirToAdd) && File.Exists(Path.Combine(dirToAdd, "__init__.py"))) {
                    dirToAdd = Path.GetDirectoryName(dirToAdd);
                }

                // Search paths are 'friendly' to ensure that they are never blank and don't end in
                // a directory separator.
                AddSearchPathEntry(CommonUtils.CreateFriendlyDirectoryPath(ProjectHome, dirToAdd));
            }

            base.LinkFileAdded(filename);
        }

        protected override Guid[] GetConfigurationIndependentPropertyPages() {
            return new[] { 
                GetGeneralPropertyPageType().GUID,
                typeof(PythonDebugPropertyPage).GUID,
                typeof(PublishPropertyPage).GUID
            };
        }

        /// <summary>
        /// Evaluates if a file is a current language code file based on is extension
        /// </summary>
        /// <param name="strFileName">The filename to be evaluated</param>
        /// <returns>true if is a code file</returns>
        public override bool IsCodeFile(string strFileName) {
            return IsPythonFile(strFileName);
        }

        public override string[] CodeFileExtensions {
            get {
                return new[] { PythonConstants.FileExtension, PythonConstants.WindowsFileExtension };
            }
        }

        internal static bool IsPythonFile(string strFileName) {
            var ext = Path.GetExtension(strFileName);

            return String.Equals(ext, PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, PythonConstants.WindowsFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public override Type GetProjectFactoryType() {
            return typeof(PythonProjectFactory);
        }

        public override string GetProjectName() {
            return "PythonProject";
        }

        public override string GetFormatList() {
            return String.Format(CultureInfo.CurrentCulture, ".py"/*Resources.ProjectFileExtensionFilter*/, "\0", "\0");
        }

        public override Type GetGeneralPropertyPageType() {
            return typeof(PythonGeneralPropertyPage);
        }

        public override Type GetEditorFactoryType() {
            return typeof(PythonEditorFactory);
        }

        public override Type GetLibraryManagerType() {
            return typeof(IPythonLibraryManager);
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new PythonProjectNodeProperties(this);
        }

        public override CommonProjectConfig MakeConfiguration(string activeConfigName) {
            return new PythonProjectConfig(this, activeConfigName);
        }

        protected internal override FolderNode CreateFolderNode(string path, ProjectElement element) {
            return new PythonFolderNode(this, path, element);
        }

        protected override void Reload() {
            _searchPathContainer = new CommonSearchPathContainerNode(this);
            this.AddChild(_searchPathContainer);
            RefreshCurrentWorkingDirectory();
            RefreshSearchPaths();

            OnProjectPropertyChanged += PythonProjectNode_OnProjectPropertyChanged;
            base.Reload();
        }

        private void RefreshCurrentWorkingDirectory() {
            try {
                IsRefreshing = true;
                string projHome = ProjectHome;
                string workDir = GetWorkingDirectory();

                //Refresh CWD node
                bool needCWD = !CommonUtils.IsSameDirectory(projHome, workDir);
                var cwdNode = FindImmediateChild<CurrentWorkingDirectoryNode>(_searchPathContainer);
                if (needCWD) {
                    if (cwdNode == null) {
                        //No cwd node yet
                        _searchPathContainer.AddChild(new CurrentWorkingDirectoryNode(this, workDir));
                    } else if (!CommonUtils.IsSameDirectory(cwdNode.Url, workDir)) {
                        //CWD has changed, recreate the node
                        cwdNode.Remove(false);
                        _searchPathContainer.AddChild(new CurrentWorkingDirectoryNode(this, workDir));
                    }
                } else {
                    //No need to show CWD, remove if exists
                    if (cwdNode != null) {
                        cwdNode.Remove(false);
                    }
                }
            } finally {
                IsRefreshing = false;
            }
        }

        private void RefreshSearchPaths() {
            try {
                IsRefreshing = true;

                string projHome = ProjectHome;
                string workDir = GetWorkingDirectory();
                IList<string> searchPath = ParseSearchPath();

                //Refresh regular search path nodes

                //We need to update search path nodes according to the search path property.
                //It's quite expensive to remove all and build all nodes from scratch, 
                //so we are going to perform some smarter update.
                //We are looping over paths in the search path and if a corresponding node
                //exists, we only update its index (sort order), creating new node otherwise.
                //At the end all nodes that haven't been updated have to be removed - they are
                //not in the search path anymore.
                var searchPathNodes = new List<CommonSearchPathNode>();
                this.FindNodesOfType<CommonSearchPathNode>(searchPathNodes);
                bool[] updatedNodes = new bool[searchPathNodes.Count];
                int index;
                for (int i = 0; i < searchPath.Count; i++) {
                    string path = searchPath[i];
                    //ParseSearchPath() must resolve all paths
                    Debug.Assert(Path.IsPathRooted(path));
                    var node = FindSearchPathNodeByPath(searchPathNodes, path, out index);
                    if (node != null) {
                        //existing path, update index (sort order)
                        node.Index = i;
                        updatedNodes[index] = true;
                    } else {
                        //new path - create new node
                        _searchPathContainer.AddChild(new CommonSearchPathNode(this, path, i));
                    }
                }

                //Refresh nodes and remove non-updated ones
                for (int i = 0; i < searchPathNodes.Count; i++) {
                    if (!updatedNodes[i]) {
                        searchPathNodes[i].Remove();
                    }
                }
            } finally {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// Returns first immediate child node (non-recursive) of a given type.
        /// </summary>
        private static T FindImmediateChild<T>(HierarchyNode parent)
            where T : HierarchyNode {
            for (HierarchyNode n = parent.FirstChild; n != null; n = n.NextSibling) {
                if (n is T) {
                    return (T)n;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds Search Path node by a given search path and returns it along with the node's index. 
        /// </summary>
        private CommonSearchPathNode FindSearchPathNodeByPath(IList<CommonSearchPathNode> nodes, string path, out int index) {
            index = 0;
            for (int j = 0; j < nodes.Count; j++) {
                if (CommonUtils.IsSameDirectory(nodes[j].Url, path)) {
                    index = j;
                    return nodes[j];
                }
            }
            return null;
        }

        private void PythonProjectNode_OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs e) {
            switch (e.PropertyName) {
                case CommonConstants.StartupFile:
                    var genProp = GeneralPropertyPageControl;
                    if (genProp != null) {
                        genProp.StartupFile = e.NewValue;
                    }
                    break;
                case CommonConstants.WorkingDirectory:
                    RefreshCurrentWorkingDirectory();
                    genProp = GeneralPropertyPageControl;
                    if (genProp != null) {
                        genProp.WorkingDirectory = e.NewValue;
                    }
                    break;
                case CommonConstants.SearchPath:
                    RefreshSearchPaths();

                    // we need to remove old files from the analyzer and add the new files
                    HashSet<string> oldDirs = new HashSet<string>(ParseSearchPath(e.OldValue), StringComparer.OrdinalIgnoreCase);
                    HashSet<string> newDirs = new HashSet<string>(ParseSearchPath(e.NewValue), StringComparer.OrdinalIgnoreCase);

                    // figure out all the possible directory names we could be removing...
                    foreach (var fileProject in _analyzer.LoadedFiles) {
                        string file = fileProject.Key;
                        IProjectEntry projectEntry = fileProject.Value;
                        string searchPathEntry = GetSearchPathEntry(fileProject.Value);
                        if (searchPathEntry != null && !newDirs.Contains(searchPathEntry)) {
                            _analyzer.UnloadFile(projectEntry);
                        }
                    }

                    // find the values only in the old list, and let the analyzer know it shouldn't be watching those dirs
                    oldDirs.ExceptWith(newDirs);
                    foreach (var dir in oldDirs) {
                        _analyzer.StopAnalyzingDirectory(dir);
                    }

                    AnalyzeSearchPaths(newDirs);
                    break;
            }

            var debugProp = DebugPropertyPage;
            if (debugProp != null) {
                ((PythonDebugPropertyPageControl)debugProp.Control).ReloadSetting(e.PropertyName);
            }
        }

        private void AnalyzeSearchPaths(IEnumerable<string> newDirs) {
            // now add all of the missing files, any dups will automatically not be re-analyzed
            foreach (var dir in newDirs) {
#if DEV11
                // If it's a file and not a directory, parse it as a .zip file in accordance with PEP 273.
                if (File.Exists(dir)) {
                    _analyzer.AnalyzeZipArchive(dir, onFileAnalyzed: entry => SetSearchPathEntry(entry, dir));
                    continue;
                }
#endif
                _analyzer.AnalyzeDirectory(dir, onFileAnalyzed: entry => SetSearchPathEntry(entry, dir));
            }
        }

        private PythonGeneralPropertyPageControl GeneralPropertyPageControl {
            get {
                if (PropertyPage != null && PropertyPage.Control != null) {
                    return (PythonGeneralPropertyPageControl)PropertyPage.Control;
                }

                return null;
            }
        }

        internal PythonDebugPropertyPage DebugPropertyPage {
            get {
                return _debugPropPage;
            }
            set {
                _debugPropPage = value;
            }
        }

        protected override internal Microsoft.Windows.Design.Host.DesignerContext DesignerContext {
            get {
                if (_designerContext == null) {
                    _designerContext = new DesignerContext();
                    //Set the RuntimeNameProvider so the XAML designer will call it when items are added to
                    //a design surface. Since the provider does not depend on an item context, we provide it at 
                    //the project level.
                    // This is currently disabled because we don't successfully serialize to the remote domain
                    // and the default name provider seems to work fine.  Likely installing our assembly into
                    // the GAC or implementing an IsolationProvider would solve this.
                    //designerContext.RuntimeNameProvider = new PythonRuntimeNameProvider();
                }
                return _designerContext;
            }
        }

        public PythonAnalyzer GetProjectAnalyzer() {
            return GetAnalyzer().Project;
        }

        public override IProjectLauncher GetLauncher() {
            return PythonToolsPackage.GetLauncher(this);
        }

        public override void BeforeClose() {
            if (this.ErrorFiles.Count > 0) {
                var analyzer = GetAnalyzer();
                analyzer.BeginUnload();
                try {
                    foreach (var node in EnumNodesOfType<PythonFileNode>()) {
                        analyzer.UnloadFile(node.GetAnalysis());
                    }
                } finally {
                    analyzer.EndUnload();
                }
            }

            DisposeInterpreter();

            if (_defaultInterpreter) {
                PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterChanged -= DefaultInterpreterChanged;
            }

            if (_analyzer != null) {
                _analyzer.Dispose();
                _analyzer = null;
            }
        }

        private void DisposeInterpreter() {
            var dispInterp = _interpreter as IDisposable;
            if (dispInterp != null) {
                dispInterp.Dispose();
            }
            _interpreter = null;
        }

        public IPythonInterpreter GetInterpreter() {
            if (_interpreter == null) {
                CreateInterpreter();
            }
            return _interpreter;
        }

        public VsProjectAnalyzer GetAnalyzer() {
            if (_analyzer == null) {
                _analyzer = CreateAnalyzer();
                AnalyzeSearchPaths(ParseSearchPath());
            }
            return _analyzer;
        }

        private VsProjectAnalyzer CreateAnalyzer() {
            // check to see if we should share our analyzer with another project in the same solution.  This enables
            // refactoring, find all refs, and intellisense across projects.
            var vsSolution = (IVsSolution)GetService(typeof(SVsSolution));
            var guid = new Guid(PythonConstants.ProjectFactoryGuid);
            IEnumHierarchies hierarchies;
            ErrorHandler.ThrowOnFailure((vsSolution.GetProjectEnum((uint)(__VSENUMPROJFLAGS.EPF_MATCHTYPE | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION), ref guid, out hierarchies)));
            IVsHierarchy[] hierarchy = new IVsHierarchy[1];
            uint fetched;
            var curFactory = GetInterpreterFactory();
            while (ErrorHandler.Succeeded(hierarchies.Next(1, hierarchy, out fetched)) && fetched == 1) {
                var pyProj = hierarchy[0].GetProject().GetPythonProject();

                if (pyProj != this &&
                    pyProj._analyzer != null &&
                    pyProj._analyzer.InterpreterFactory == curFactory) {
                    // we have the same interpreter, we'll share analysis engines across projects.
                    return pyProj._analyzer;
                }
            }

            var model = GetService(typeof(SComponentModel)) as IComponentModel;
            return new VsProjectAnalyzer(GetInterpreter(), GetInterpreterFactory(), model.GetAllPythonInterpreterFactories(), model.GetService<IErrorProviderFactory>(), this);
        }

        private void CreateInterpreter() {
            var fact = GetInterpreterFactory();

            _interpreter = fact.CreateInterpreter();
        }

        /// <summary>
        /// File names within the project which contain errors.
        /// </summary>
        public HashSet<string> ErrorFiles {
            get {
                return _errorFiles;
            }
        }

        internal IPythonInterpreterFactory GetInterpreterFactory() {
            var interpreterId = GetProjectProperty(PythonConstants.InterpreterId, false);
            var interpreterVersion = GetProjectProperty(PythonConstants.InterpreterVersion, false);

            var model = GetService(typeof(SComponentModel)) as IComponentModel;

            var allFactories = model.GetAllPythonInterpreterFactories();
            var fact = allFactories.GetInterpreterFactory(interpreterId, interpreterVersion);

            if (fact == null) {
                fact = allFactories.GetDefaultInterpreter();
                if (!_defaultInterpreter) {
                    // http://pytools.codeplex.com/workitem/643
                    // Don't hook the event multiple times

                    _defaultInterpreter = true;

                    PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterChanged += DefaultInterpreterChanged;
                }
            } else {
                if (_defaultInterpreter) {
                    PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterChanged -= DefaultInterpreterChanged;
                }
                _defaultInterpreter = false;
            }

            PythonToolsPackage.EnsureCompletionDb(fact);

            return fact;
        }

        private void DefaultInterpreterChanged(object sender, EventArgs e) {
            ClearInterpreter();
        }

        /// <summary>
        /// Called when default interpreter is changed.  A new interpreter will be lazily created when needed.
        /// </summary>
        internal void ClearInterpreter() {
            DisposeInterpreter();

            var analyzer = CreateAnalyzer();

            Reanalyze(this, analyzer);
            analyzer.SwitchAnalyzers(_analyzer);
            AnalyzeSearchPaths(ParseSearchPath());

            _analyzer = analyzer;
        }

        private void Reanalyze(HierarchyNode node, VsProjectAnalyzer newAnalyzer) {
            if (node != null) {
                for (var child = node.FirstChild; child != null; child = child.NextSibling) {
                    if (child is FileNode) {
                        newAnalyzer.AnalyzeFile(child.Url);
                    }

                    Reanalyze(child, newAnalyzer);
                }
            }
        }

        public override ReferenceNode CreateReferenceNodeForFile(string filename) {
            var interp = this.GetInterpreter() as IPythonInterpreter2;
            CancellationTokenSource cancelSource = new CancellationTokenSource();
            var task = interp.AddReferenceAsync(new ProjectReference(filename, ProjectReferenceKind.ExtensionModule), cancelSource.Token);

            // try to complete synchronously w/o flashing the dialog...
            if (!task.Wait(100)) {
                var progress = new TaskProgressBar(task, cancelSource, "Waiting for analysis of extension module to complete...");
                if (progress.ShowDialog() != true) {
                    // user cancelled.
                    return null;
                }
            }

            var exception = task.Exception;
            if (exception != null) {
                string msg = GetErrorMessage(exception);

                string fullMsg = String.Format("Cannot add reference to {0}:", filename);
                if (msg != null) {
                    fullMsg = fullMsg + "\r\n\r\n" + msg;
                }
                MessageBox.Show(fullMsg);
            } else {
                return new PythonExtensionReferenceNode(this, filename);
            }

            return null;
        }

        private static string GetErrorMessage(AggregateException exception) {
            string msg = null;
            foreach (var inner in exception.InnerExceptions) {
                if (inner is AggregateException) {
                    msg = GetErrorMessage((AggregateException)inner);
                } else if (msg == null || inner is CannotAnalyzeExtensionException) {
                    msg = inner.Message;
                }
            }
            return msg;
        }

        class OutputDataReceiver {
            public readonly StringBuilder Received = new StringBuilder();

            public void OutputDataReceived(object sender, DataReceivedEventArgs e) {
                Received.Append(e.Data);
            }

        }

        protected override string AddReferenceExtensions {
            get {
                return "Python Extension Modules (*.dll;*.pyd)\0*.dll;*.pyd\0All Files (*.*)\0*.*\0";
            }
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == Microsoft.PythonTools.Project.VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.ECMD_PUBLISHSELECTION:
                    case VsCommands2K.ECMD_PUBLISHSLNCTX:
                        Publish(PublishProjectOptions.Default, true);
                        return VSConstants.S_OK;
                    case CommonConstants.OpenFolderInExplorerCmdId:
                        Process.Start(this.ProjectHome);
                        return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch ((int)cmd) {
                    case CommonConstants.AddSearchPathCommandId:
                    case CommonConstants.AddSearchPathZipCommandId:
                    case CommonConstants.StartWithoutDebuggingCmdId:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        #region IPythonProject Members

        string IPythonProject.ProjectName {
            get {
                return Caption;
            }
        }

        string IPythonProject.ProjectDirectory {
            get {
                return ProjectHome;
            }
        }

        string IPythonProject.GetProperty(string name) {
            return GetProjectProperty(name, true);
        }

        void IPythonProject.SetProperty(string name, string value) {
            SetProjectProperty(name, value);
        }

        string IPythonProject.GetStartupFile() {
            return base.GetStartupFile();
        }

        IPythonInterpreterFactory IPythonProject.GetInterpreterFactory() {
            return this.GetInterpreterFactory();
        }

        bool IPythonProject.Publish(PublishProjectOptions options) {
            return base.Publish(options, false);
        }

        #endregion

        #region IPythonProject2 Members

        string IPythonProject2.GetUnevaluatedProperty(string name) {
            return base.GetUnevaluatedProperty(name);
        }

        #endregion
    }
}