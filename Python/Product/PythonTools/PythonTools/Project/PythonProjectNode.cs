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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Microsoft.Windows.Design.Host;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    internal class PythonProjectNode : CommonProjectNode, IPythonProject {
        // For files that are analyzed because they were directly or indirectly referenced in the search path, store the information
        // about the directory from the search path that referenced them in IProjectEntry.Properties[_searchPathEntryKey], so that
        // they can be located and removed when that directory is removed from the path.
        private static readonly object _searchPathEntryKey = new { Name = "SearchPathEntry" };

        private DesignerContext _designerContext;
        private IPythonInterpreter _interpreter;
        private VsProjectAnalyzer _analyzer;
        private readonly HashSet<string> _warningFiles = new HashSet<string>();
        private readonly HashSet<string> _errorFiles = new HashSet<string>();
        private bool _defaultInterpreter;
        private PythonDebugPropertyPage _debugPropPage;
        private CommonSearchPathContainerNode _searchPathContainer;
        private List<VirtualEnvRequestHandler> _virtualEnvCreationRequests;

        public PythonProjectNode(CommonProjectPackage package)
            : base(package, Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList))) {

            Type projectNodePropsType = typeof(PythonProjectNodeProperties);
            AddCATIDMapping(projectNodePropsType, projectNodePropsType.GUID);
        }

        protected override Stream ProjectIconsImageStripStream {
            get {
                return typeof(ProjectNode).Assembly.GetManifestResourceStream("Microsoft.PythonTools.Project.Resources.imagelis.bmp");
            }
        }

        private static string GetSearchPathEntry(IProjectEntry entry) {
            object result;
            entry.Properties.TryGetValue(_searchPathEntryKey, out result);
            return (string)result;
        }

        private static void SetSearchPathEntry(IProjectEntry entry, string value) {
            entry.Properties[_searchPathEntryKey] = value;
        }

        public override CommonFileNode CreateCodeFileNode(ProjectElement item) {
            return new PythonFileNode(this, item);
        }

        public override CommonFileNode CreateNonCodeFileNode(ProjectElement item) {
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

                AddSearchPathEntry(dirToAdd);
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
            return PythonConstants.ProjectFileFilter;
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

        protected internal override FolderNode CreateFolderNode(ProjectElement element) {
            return new PythonFolderNode(this, element);
        }

        protected override void Reload() {
            _searchPathContainer = new CommonSearchPathContainerNode(this);
            this.AddChild(_searchPathContainer);
            RefreshCurrentWorkingDirectory();
            RefreshSearchPaths();

            OnProjectPropertyChanged += PythonProjectNode_OnProjectPropertyChanged;
            base.Reload();
        }

        protected internal override void ProcessReferences() {
            base.ProcessReferences();

            var virtualEnv = GetVirtualEnvContainerNode();
            if (virtualEnv == null) {
                virtualEnv = new VirtualEnvContainerNode(this);
                AddChild(virtualEnv);
            }

            foreach (var buildItem in BuildProject.GetItems(PythonConstants.VirtualEnvItemType)) {
                virtualEnv.AddChild(new VirtualEnvNode(this, buildItem));
            }
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
#if DEV11_OR_LATER
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

        VsProjectAnalyzer IPythonProject.GetProjectAnalyzer() {
            return GetAnalyzer();
        }

        public event EventHandler ProjectAnalyzerChanged;

        public override IProjectLauncher GetLauncher() {
            return PythonToolsPackage.GetLauncher(this);
        }

        public override void BeforeClose() {
            if (_analyzer != null) {
                _analyzer.Cancel();

                if (this.WarningFiles.Count > 0 || this.ErrorFiles.Count > 0) {
                    foreach (var node in EnumNodesOfType<PythonFileNode>()) {
                        _analyzer.UnloadFile(node.GetAnalysis(), suppressUpdate: true);
                    }
                }

                _analyzer.Dispose();
                _analyzer = null;
            }

            DisposeInterpreter();

            if (_defaultInterpreter) {
                var interpService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
                interpService.DefaultInterpreterChanged -= DefaultInterpreterChanged;
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
            if (vsSolution != null) {
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
            }

            var model = PythonToolsPackage.ComponentModel;
            var interpService = model.GetService<IInterpreterOptionsService>();
            return new VsProjectAnalyzer(
                GetInterpreter(),
                GetInterpreterFactory(),
                interpService.Interpreters.ToArray(),
                model.GetService<IErrorProviderFactory>(),
                this);
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

        /// <summary>
        /// File names within the project which contain warnings.
        /// </summary>
        public HashSet<string> WarningFiles {
            get {
                return _warningFiles;
            }
        }

        internal IPythonInterpreterFactory GetInterpreterFactory() {
            var interpreterId = GetProjectProperty(PythonConstants.InterpreterId, false);
            var interpreterVersion = GetProjectProperty(PythonConstants.InterpreterVersion, false);
            Guid id;
            Version version;

            var interpService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();

            IPythonInterpreterFactory fact = null;
            if (Guid.TryParse(interpreterId, out id) && Version.TryParse(interpreterVersion, out version)) {
                fact = interpService.FindInterpreter(id, version);
            }

            if (fact == null) {
                fact = interpService.DefaultInterpreter;
                if (!_defaultInterpreter) {
                    // http://pytools.codeplex.com/workitem/643
                    // Don't hook the event multiple times

                    _defaultInterpreter = true;

                    interpService.DefaultInterpreterChanged += DefaultInterpreterChanged;
                }
            } else {
                if (_defaultInterpreter) {
                    interpService.DefaultInterpreterChanged -= DefaultInterpreterChanged;
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

            var analyzerChanged = ProjectAnalyzerChanged;
            if (analyzerChanged != null) {
                analyzerChanged(this, EventArgs.Empty);
            }
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
            var interp = this.GetInterpreter();
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

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
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

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
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

        string IPythonProject.GetUnevaluatedProperty(string name) {
            return base.GetUnevaluatedProperty(name);
        }

        #endregion

        internal unsafe int AddSearchPathZip() {
            var uiShell = GetService(typeof(SVsUIShell)) as IVsUIShell;
            if (uiShell == null) {
                return VSConstants.S_FALSE;
            }

            var fileNameBuf = stackalloc char[NativeMethods.MAX_PATH];
            var ofn = new[] {
                new VSOPENFILENAMEW {
                    lStructSize = (uint)Marshal.SizeOf(typeof(VSOPENFILENAMEW)),
                    pwzDlgTitle = DynamicProjectSR.GetString(DynamicProjectSR.SelectZipFileForSearchPath),
                    nMaxFileName = NativeMethods.MAX_PATH,
                    pwzFileName = (IntPtr)fileNameBuf,
                    pwzInitialDir = ProjectHome,
                    pwzFilter = "Zip Archives\0*.zip\0All Files\0*.*\0"
                }
            };
            uiShell.GetDialogOwnerHwnd(out ofn[0].hwndOwner);

            var hr = uiShell.GetOpenFileNameViaDlg(ofn);
            if (hr == VSConstants.OLE_E_PROMPTSAVECANCELLED) {
                return VSConstants.S_OK;
            }
            ErrorHandler.ThrowOnFailure(hr);

            string fileName = new string(fileNameBuf);
            AddSearchPathEntry(fileName);
            return VSConstants.S_OK;
        }

        #region Virtual Env support

        internal int CreateVirtualEnv() {
            var view = new CreateVirtualEnvironmentView(true);
            if (view.AvailableInterpreters.Count == 0) {
                MessageBox.Show("There are no configured interpreters available to create a virtual environment.\r\n\r\nPlease install a Python interpreter or register one in Tools->Options->Python Tools->Interpreters");
                return VSConstants.S_OK;
            }

            const string baseName = "env";
            view.Location = ProjectHome;
            view.Name = baseName;
            if (Directory.Exists(Path.Combine(view.Location, view.Name))) {
                for (int i = 1; i < Int32.MaxValue; i++) {
                    if (!Directory.Exists(Path.Combine(view.Location, baseName + i))) {
                        view.Name = baseName + i;
                        break;
                    }
                }
            }

            SelectDefaultVirtualEnvInterpreter(view);

            var createVirtualEnv = new CreateVirtualEnvironment(view);
            var res = createVirtualEnv.ShowDialog();
            if (res != null && res.Value) {
                var psi = new ProcessStartInfo(view.Interpreter.Path, "-m virtualenv " + view.Name);
                psi.WorkingDirectory = view.Location;

                EnqueueVirtualEnvRequest(
                    psi, 
                    "Creating virtual environment...", 
                    "Virtual environment created successfully", 
                    "Error in creating virtual environment: {0}", 
                    () => AddVirtualEnvPath(Path.Combine(view.Location, view.Name), view.Interpreter.Version, view.Interpreter.Id)
                );
            }
            return VSConstants.S_OK;
        }

        private void SelectDefaultVirtualEnvInterpreter(CreateVirtualEnvironmentView view) {
            var curInterpreter = GetInterpreterFactory();
            foreach (var interpreter in view.AvailableInterpreters) {
                if (interpreter.Id == curInterpreter.Id &&
                    interpreter.Version == curInterpreter.Configuration.Version) {
                    view.Interpreter = interpreter;
                    break;
                }
            }
        }

        internal void EnqueueVirtualEnvRequest(ProcessStartInfo psi, string initialMsg, string success, string error, Action onSuccess, Action onError = null) {
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            if (_virtualEnvCreationRequests == null) {
                _virtualEnvCreationRequests = new List<VirtualEnvRequestHandler>();
            }

            // make sure we have the General pane, it's not created for us in VS 2010
            IVsOutputWindow outputWindow = GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            IVsOutputWindowPane pane;
            outputWindow.CreatePane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "General", 1, 0);
            outputWindow.GetPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, out pane);
            
            var process = Process.Start(psi);
            var handler = new VirtualEnvRequestHandler(
                pane,
                process,
                this,
                onSuccess,
                onError,
                TaskScheduler.FromCurrentSynchronizationContext(),
                success,
                error);
            
            lock (_virtualEnvCreationRequests) {
                // keep the Process object alive so we receive events even after GC
                _virtualEnvCreationRequests.Add(handler);
            }
            
            process.Exited += handler.VirtualEnvCreationExited;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += handler.CreateVirtualEnvOutputDataReceived;
            process.ErrorDataReceived += handler.CreateVirtualEnvOutputDataReceived;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));
            statusBar.SetText(initialMsg + SeeOutputWindowForMoreDetails);
            
            pane.Activate();
            pane.OutputString(initialMsg + Environment.NewLine);
        }

        const string SeeOutputWindowForMoreDetails = " (See Output Window for more details)";

        class VirtualEnvRequestHandler {
            private readonly string _success, _error;
            private readonly PythonProjectNode _node;
            private readonly TaskScheduler _scheduler;
            private readonly Action _onSuccess, _onError;
            private readonly Process _process;
            private readonly IVsOutputWindowPane _outPane;

            public VirtualEnvRequestHandler(IVsOutputWindowPane outPane, Process process, PythonProjectNode node, Action onSuccess, Action onError, TaskScheduler scheduler, string success, string error) {
                _outPane = outPane;
                _process = process;
                _node = node;
                _scheduler = scheduler;
                _onSuccess = onSuccess;
                _onError = onError;
                _scheduler = scheduler;
                _success = success;
                _error = error;
            }

            public void VirtualEnvCreationExited(object sender, EventArgs e) {
                lock (_node._virtualEnvCreationRequests) {
                    // no longer keep the process object alive.
                    _node._virtualEnvCreationRequests.Remove(this);
                }

                var proc = (Process)sender;

                var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));                

                if (proc.ExitCode == 0) {
                    statusBar.SetText(_success + SeeOutputWindowForMoreDetails);
                    if (_outPane != null) {
                        _outPane.OutputStringThreadSafe(_success + Environment.NewLine);
                    }
                    _scheduler.StartNew(_onSuccess).Wait();
                } else {
                    var msg = String.Format(_error, proc.ExitCode);
                    statusBar.SetText(_error + SeeOutputWindowForMoreDetails);
                    if (_outPane != null) {
                        _outPane.OutputStringThreadSafe(msg + Environment.NewLine);
                    }
                    if (_onError != null) {
                        _scheduler.StartNew(_onError).Wait();
                    }
                }
            }

            public void CreateVirtualEnvOutputDataReceived(object sender, DataReceivedEventArgs e) {
                if (_outPane != null && e.Data != null) {
                    _outPane.OutputStringThreadSafe(e.Data + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// Executes Add Search Path menu command.
        /// </summary>        
        internal int AddVirtualEnv() {
            var view = new CreateVirtualEnvironmentView(false);
            if (view.AvailableInterpreters.Count == 0) {
                MessageBox.Show("There are no configured base interpreters available to register a virtual environment.\r\n\r\nPlease install a Python interpreter or register one in Tools->Options->Python Tools->Interpreters");
                return VSConstants.S_OK;
            }

            SelectDefaultVirtualEnvInterpreter(view);
            view.Location = ProjectHome;

            var createVirtualEnv = new CreateVirtualEnvironment(view);
            var res = createVirtualEnv.ShowDialog();
            if (res ?? false) {
                AddVirtualEnvPath(view.Location, view.Interpreter.Version, view.Interpreter.Id);
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Adds new search path to the SearchPath project property.
        /// </summary>
        internal void AddVirtualEnvPath(string newpath, Version version, Guid interpreterId) {
            Utilities.ArgumentNotNull("newpath", newpath);

            var relativePath = CommonUtils.TrimEndSeparator(CommonUtils.GetRelativeDirectoryPath(ProjectHome, CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, newpath)));
            var items = BuildProject.GetItems(PythonConstants.VirtualEnvItemType);
            foreach (var item in items) {
                if (CommonUtils.IsSameDirectory(
                    CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, item.EvaluatedInclude),
                    CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, relativePath))) {
                    // item already exists
                    return;
                }
            }

            //Make sure we can edit the project file
            if (!QueryEditProjectFile(false)) {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            var newItem = BuildProject.AddItem(PythonConstants.VirtualEnvItemType, relativePath).First();
            newItem.SetMetadataValue(PythonConstants.VirtualEnvInterpreterId, interpreterId.ToString());
            newItem.SetMetadataValue(PythonConstants.VirtualEnvInterpreterVersion, version.ToString());
            GetVirtualEnvContainerNode().AddChild(new VirtualEnvNode(this, newItem));
            SetProjectFileDirty(true);
        }

        /// <summary>
        /// Removes a given path from the SearchPath property.
        /// </summary>
        internal void RemoveVirtualEnvPath(string path) {
            Utilities.ArgumentNotNull("path", path);

            var relativePath = CommonUtils.TrimEndSeparator(CommonUtils.GetRelativeDirectoryPath(ProjectHome, CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, path)));
            var items = BuildProject.GetItems(PythonConstants.VirtualEnvItemType);
            foreach (var item in items) {
                if (CommonUtils.IsSameDirectory(
                    CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, item.EvaluatedInclude),
                    CommonUtils.GetAbsoluteDirectoryPath(ProjectHome, relativePath))) {
                    //Make sure we can edit the project file
                    if (!QueryEditProjectFile(false)) {
                        throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
                    }

                    BuildProject.RemoveItem(item);
                    SetProjectFileDirty(true);

                    var venvContainer = GetVirtualEnvContainerNode();
                    for (var curItem = venvContainer.FirstChild; curItem != null; curItem = curItem.NextSibling) {
                        if (((MsBuildProjectElement)curItem.ItemNode).Item.UnevaluatedInclude == item.UnevaluatedInclude) {
                            venvContainer.RemoveChild(curItem);
                            OnInvalidateItems(venvContainer);
                            break;
                        }
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Returns the reference container node.
        /// </summary>
        internal VirtualEnvContainerNode GetVirtualEnvContainerNode() {
            for (var child = this.FirstChild; child != null; child = child.NextSibling) {
                if (child is VirtualEnvContainerNode) {
                    return (VirtualEnvContainerNode)child;
                }
            }
            return null;
        }

        #endregion

        public override Guid SharedCommandGuid {
            get {
                return GuidList.guidPythonToolsCmdSet;
            }
        }
    }
}
