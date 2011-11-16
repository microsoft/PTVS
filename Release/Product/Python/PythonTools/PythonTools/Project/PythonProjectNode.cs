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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text.Adornments;
#if !DEV11
using Microsoft.Windows.Design.Host;
#endif

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    public class PythonProjectNode : CommonProjectNode, IPythonProject {
#if !DEV11
        private DesignerContext _designerContext;
#endif
        private IPythonInterpreter _interpreter;
        private ProjectAnalyzer _analyzer;
        private readonly HashSet<string> _errorFiles = new HashSet<string>();
        private bool _defaultInterpreter;
        private PythonDebugPropertyPage _debugPropPage;

        public PythonProjectNode(CommonProjectPackage package)
            : base(package, Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList))) {
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

                AddSearchPathEntry(CommonUtils.CreateFriendlyDirectoryPath(ProjectFolder, dirToAdd));
            }

            base.LinkFileAdded(filename);
        }

        /// <summary>
        /// Evaluates if a file is a current language code file based on is extension
        /// </summary>
        /// <param name="strFileName">The filename to be evaluated</param>
        /// <returns>true if is a code file</returns>
        public override bool IsCodeFile(string strFileName) {
            return IsPythonFile(strFileName);
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
            OnProjectPropertyChanged += PythonProjectNode_OnProjectPropertyChanged;
            base.Reload();
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
                    genProp = GeneralPropertyPageControl;
                    if (genProp != null) {
                        genProp.WorkingDirectory = e.NewValue;
                    }
                    break;
                case CommonConstants.SearchPath:
                    // we need to remove old files from the analyzer and add the new files
                    HashSet<string> oldDirs = new HashSet<string>(ParseSearchPath(e.OldValue), StringComparer.OrdinalIgnoreCase);
                    HashSet<string> newDirs = new HashSet<string>(ParseSearchPath(e.NewValue), StringComparer.OrdinalIgnoreCase);
                    // figure out all the possible directory names we could be removing...
                    foreach (var fileProject in _analyzer.LoadedFiles) {
                        var file = fileProject.Key;
                        var projectEntry = fileProject.Value;

                        // remove the file if directly included, or if included via a package or series of packages.
                        string dirName = Path.GetDirectoryName(file);
                        do {
                            string tmpDir = dirName;
                            if (!tmpDir.EndsWith("\\")) {
                                tmpDir = tmpDir + "\\";
                            }
                            if (oldDirs.Contains(tmpDir)) {
                                if (!newDirs.Contains(tmpDir)) {
                                    // path removed
                                    _analyzer.UnloadFile(projectEntry);
                                    break;
                                }
                            }
                            dirName = Path.GetDirectoryName(dirName);
                        } while (dirName != null && File.Exists(Path.Combine(dirName, "__init__.py")));
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
                _analyzer.AnalyzeDirectory(dir);
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

#if !DEV11
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
#endif

        public override IProjectLauncher GetLauncher() {
            var compModel = PythonToolsPackage.ComponentModel;
            var launchers = compModel.GetExtensions<IPythonLauncherProvider>();
            var launchProvider = GetProjectProperty(PythonConstants.LaunchProvider, false);

            IProjectLauncher res = null;
            IPythonLauncherProvider defaultLaunchProvider = null;
            foreach (var launcher in launchers) {
                if (launcher.Name == launchProvider) {
                    res = launcher.CreateLauncher(this);
                    break;
                } else if (res == null && launcher.Name == DefaultLauncherProvider.DefaultLauncherDescription) {
                    defaultLaunchProvider = launcher;
                }
            }

            if (res == null) {
                // no launcher configured, use the default one.
                Debug.Assert(defaultLaunchProvider != null);
                res = defaultLaunchProvider.CreateLauncher(this);
            }

            return res;
        }

        public override void BeforeClose() {
            if (this.ErrorFiles.Count > 0) {
                var analyzer = GetAnalyzer();
                foreach (var node in EnumNodesOfType<PythonFileNode>()) {
                    analyzer.UnloadFile(node.GetAnalysis());
                }
            }

            if (_defaultInterpreter) {
                PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterChanged -= DefaultInterpreterChanged;
            }

            if (_analyzer != null) {
                _analyzer.Dispose();
                _analyzer = null;
            }            
        }

        public IPythonInterpreter GetInterpreter() {
            if (_interpreter == null) {
                CreateInterpreter();
            }
            return _interpreter;
        }

        internal ProjectAnalyzer GetAnalyzer() {
            if (_analyzer == null) {
                _analyzer = CreateAnalyzer();
                AnalyzeSearchPaths(ParseSearchPath());
            }
            return _analyzer;
        }

        private ProjectAnalyzer CreateAnalyzer() {
            var model = GetService(typeof(SComponentModel)) as IComponentModel;
            return new ProjectAnalyzer(GetInterpreter(), GetInterpreterFactory(), model.GetAllPythonInterpreterFactories(), model.GetService<IErrorProviderFactory>(), this);
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
                _defaultInterpreter = true;

                PythonToolsPackage.Instance.InterpreterOptionsPage.DefaultInterpreterChanged += DefaultInterpreterChanged;
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
            _interpreter = null;

            var analyzer = CreateAnalyzer();

            Reanalyze(this, analyzer);
            analyzer.SwitchAnalyzers(_analyzer);
            AnalyzeSearchPaths(ParseSearchPath());

            _analyzer = analyzer;
        }

        private void Reanalyze(HierarchyNode node, ProjectAnalyzer newAnalyzer) {
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
                return "*.dll;*.pyd";
            }
        }

        #region IPythonProject Members

        string IPythonProject.ProjectName {
            get {
                return Caption;
            }
        }

        string IPythonProject.ProjectDirectory {
            get {
                return ProjectDir;
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
    }
}