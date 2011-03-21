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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.Windows.Design.Host;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

namespace Microsoft.PythonTools.Project {
    [Guid(PythonConstants.ProjectNodeGuid)]
    public class PythonProjectNode : CommonProjectNode, IPythonProject {
        private DesignerContext _designerContext;
        private IPythonInterpreter _interpreter;
        private ProjectAnalyzer _analyzer;
        private readonly HashSet<string> _errorFiles = new HashSet<string>();

        public PythonProjectNode(CommonProjectPackage package)
            : base(package, Utilities.GetImageList(typeof(PythonProjectNode).Assembly.GetManifestResourceStream(PythonConstants.ProjectImageList))) {
        }

        public override CommonFileNode CreateCodeFileNode(ProjectElement item) {
            return new PythonFileNode(this, item);
        }

        public override CommonFileNode CreateNonCodeFileNode(ProjectElement item) {
            return new PythonNonCodeFileNode(this, item);
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
            OnProjectPropertyChanged += new EventHandler<ProjectPropertyChangedArgs>(PythonProjectNode_OnProjectPropertyChanged);
            base.Reload();
        }

        void PythonProjectNode_OnProjectPropertyChanged(object sender, ProjectPropertyChangedArgs e) {            
            if (e.PropertyName == CommonConstants.StartupFile) {
                if (this.PropertyPage != null && this.PropertyPage.Control != null) {
                    var ctrl = (PythonGeneralyPropertyPageControl)this.PropertyPage.Control;
                    ctrl.StartupFile = e.NewValue;
                }
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
            }
            return _analyzer;
        }

        private ProjectAnalyzer CreateAnalyzer() {
            var model = GetService(typeof(SComponentModel)) as IComponentModel;
            return new ProjectAnalyzer(GetInterpreter(), GetInterpreterFactory(), model.GetService<IErrorProviderFactory>(), this);
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

        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {

                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.ADDREFERENCE:
                        result |= QueryStatusResult.NOTSUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;

                }
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        protected override QueryStatusResult QueryStatusCommandFromOleCommandTarget(Guid cmdGroup, uint cmd, out bool handled) {
            if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
                switch ((VsCommands2K)cmd) {
                    case VsCommands2K.ADDREFERENCE:
                        handled = true;
                        return QueryStatusResult.NOTSUPPORTED | QueryStatusResult.INVISIBLE;
                }
            }

            return base.QueryStatusCommandFromOleCommandTarget(cmdGroup, cmd, out handled);
        }

        internal IPythonInterpreterFactory GetInterpreterFactory() {
            var interpreterId = GetProjectProperty(PythonConstants.InterpreterId, false);
            var interpreterVersion = GetProjectProperty(PythonConstants.InterpreterVersion, false);

            var model = GetService(typeof(SComponentModel)) as IComponentModel;

            var allFactories = model.GetAllPythonInterpreterFactories();
            var fact = allFactories.GetInterpreterFactory(interpreterId, interpreterVersion);

            if (fact == null) {
                fact = PythonToolsPackage.Instance.GetDefaultInterpreter(allFactories);
            }

            PythonToolsPackage.EnsureCompletionDb(fact);

            return fact;
        }

        /// <summary>
        /// Called when default interpreter is changed.  A new interpreter will be lazily created when needed.
        /// </summary>
        internal void ClearInterpreter() {
            _interpreter = null;

            var analyzer = CreateAnalyzer();

            Reanalyze(this, analyzer);
            analyzer.SwitchAnalyzers(_analyzer);

            _analyzer = analyzer;
        }

        private void Reanalyze(HierarchyNode node, ProjectAnalyzer newAnalyzer) {
            if (node != null) {
                for (var child = node.FirstChild; child != null; child = child.NextSibling) {
                    newAnalyzer.AnalyzeFile(node.Url);

                    Reanalyze(child, newAnalyzer);
                }
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
            return GetProjectProperty(name, false);
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