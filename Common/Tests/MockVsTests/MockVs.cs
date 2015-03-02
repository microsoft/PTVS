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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using TestUtilities;
using TestUtilities.Mocks;

namespace Microsoft.VisualStudioTools.MockVsTests {
    using Thread = System.Threading.Thread;

    public sealed class MockVs : IComponentModel, IDisposable, IVisualStudioInstance {
        internal static CachedVsInfo CachedInfo = CreateCachedVsInfo();
        public CompositionContainer Container;
        private IContentTypeRegistryService _contentTypeRegistry;
        private Dictionary<Guid, Package> _packages = new Dictionary<Guid, Package>();
        internal readonly MockVsTextManager TextManager;
        internal readonly MockActivityLog ActivityLog = new MockActivityLog();
        internal readonly MockSettingsManager SettingsManager = new MockSettingsManager();
        internal readonly MockLocalRegistry LocalRegistry = new MockLocalRegistry();
        internal readonly MockVsDebugger Debugger = new MockVsDebugger();
        internal readonly MockVsTrackProjectDocuments TrackDocs = new MockVsTrackProjectDocuments();
        internal readonly MockVsShell Shell = new MockVsShell();
        internal readonly MockVsUIShell UIShell;
        public readonly MockVsSolution Solution = new MockVsSolution();
        private readonly MockVsServiceProvider _serviceProvider;
        private readonly List<MockVsTextView> _views = new List<MockVsTextView>();
        private readonly MockVsProfferCommands _proferredCommands = new MockVsProfferCommands();
        private readonly MockOleComponentManager _compManager = new MockOleComponentManager();
        private readonly MockOutputWindow _outputWindow = new MockOutputWindow();
        private readonly MockVsBuildManagerAccessor _buildManager = new MockVsBuildManagerAccessor();
        private readonly MockUIHierWinClipboardHelper _hierClipHelper = new MockUIHierWinClipboardHelper();
        private readonly MockVsMonitorSelection _monSel;
        private readonly MockVsUIHierarchyWindow _uiHierarchy;
        private readonly MockVsQueryEditQuerySave _queryEditSave = new MockVsQueryEditQuerySave();
        private readonly MockVsRunningDocumentTable _rdt;
        private readonly MockVsUIShellOpenDocument _shellOpenDoc = new MockVsUIShellOpenDocument();
        private readonly MockVsSolutionBuildManager _slnBuildMgr = new MockVsSolutionBuildManager();
        private readonly MockVsExtensibility _extensibility  = new MockVsExtensibility();
        internal IFocusable _focused;
        private bool _shutdown;
        private AutoResetEvent _uiEvent = new AutoResetEvent(false);
        private readonly List<Action> _uiEvents = new List<Action>();

        private readonly Thread UIThread;

        public MockVs() {
            TextManager = new MockVsTextManager(this);
            Container = CreateCompositionContainer();
            var serviceProvider = _serviceProvider = Container.GetExportedValue<MockVsServiceProvider>();
            UIShell = new MockVsUIShell(this);
            _monSel = new MockVsMonitorSelection(this);
            _uiHierarchy = new MockVsUIHierarchyWindow();
            _rdt = new MockVsRunningDocumentTable(this);
            _serviceProvider.AddService(typeof(SVsTextManager), TextManager);
            _serviceProvider.AddService(typeof(SVsActivityLog), ActivityLog);
            _serviceProvider.AddService(typeof(SVsSettingsManager), SettingsManager);
            _serviceProvider.AddService(typeof(SLocalRegistry), LocalRegistry);
            _serviceProvider.AddService(typeof(SComponentModel), this);
            _serviceProvider.AddService(typeof(IVsDebugger), Debugger);
            _serviceProvider.AddService(typeof(SVsSolution), Solution);
            _serviceProvider.AddService(typeof(SVsRegisterProjectTypes), Solution);
            _serviceProvider.AddService(typeof(SVsCreateAggregateProject), Solution);
            _serviceProvider.AddService(typeof(SVsTrackProjectDocuments), TrackDocs);
            _serviceProvider.AddService(typeof(SVsShell), Shell);
            _serviceProvider.AddService(typeof(SOleComponentManager), _compManager);
            _serviceProvider.AddService(typeof(SVsProfferCommands), _proferredCommands);
            _serviceProvider.AddService(typeof(SVsOutputWindow), _outputWindow);
            _serviceProvider.AddService(typeof(SVsBuildManagerAccessor), _buildManager);
            _serviceProvider.AddService(typeof(SVsUIHierWinClipboardHelper), _hierClipHelper);
            _serviceProvider.AddService(typeof(IVsUIShell), UIShell);
            _serviceProvider.AddService(typeof(IVsMonitorSelection), _monSel);
            _serviceProvider.AddService(typeof(SVsQueryEditQuerySave), _queryEditSave);
            _serviceProvider.AddService(typeof(SVsRunningDocumentTable), _rdt);
            _serviceProvider.AddService(typeof(SVsUIShellOpenDocument), _shellOpenDoc);
            _serviceProvider.AddService(typeof(SVsSolutionBuildManager), _slnBuildMgr);
            _serviceProvider.AddService(typeof(EnvDTE.IVsExtensibility), _extensibility);

            UIShell.AddToolWindow(new Guid(ToolWindowGuids80.SolutionExplorer), new MockToolWindow(new MockVsUIHierarchyWindow()));

            UIThread = new Thread(UIThreadWorker);
            UIThread.Name = "Mock UI Thread";
            UIThread.Start();
        }

        class MockSyncContext : SynchronizationContext {
            private readonly MockVs _vs;
            public MockSyncContext(MockVs vs) {
                _vs = vs;
            }

            public override void Post(SendOrPostCallback d, object state) {
                _vs.Invoke(() => d(state));
            }

            public override void Send(SendOrPostCallback d, object state) {
                _vs.Invoke<object>(() => { d(state); return null; });
            }
        }

        private void UIThreadWorker() {
            SynchronizationContext.SetSynchronizationContext(new MockSyncContext(this));
            foreach (var package in Container.GetExportedValues<IMockPackage>()) {
                package.Initialize();
            }

            while (!_shutdown) {
                _uiEvent.WaitOne();
                Action[] events;
                do {
                    lock (_uiEvents) {
                        events = _uiEvents.ToArray();
                        _uiEvents.Clear();
                    }
                    foreach (var action in events) {
                        action();
                    }
                } while (events.Length > 0);
            }
        }

        public void Invoke(Action action) {
            if (Thread.CurrentThread == UIThread) {
                action();
                return;
            }
            lock (_uiEvents) {
                _uiEvents.Add(action);
                _uiEvent.Set();
            }
        }

        public T Invoke<T>(Func<T> func) {
            if (Thread.CurrentThread == UIThread) {
                return func();
            }

            AutoResetEvent tmp = new AutoResetEvent(false);
            T res = default(T);
            Action action = () => {
                res = func();
                tmp.Set();
            };

            lock (_uiEvents) {                
                _uiEvents.Add(action);
                _uiEvent.Set();
            }
            tmp.WaitOne();
            return res;
        }


        public IServiceContainer ServiceProvider {
            get {
                return _serviceProvider;
            }
        }

        public IComponentModel ComponentModel {
            get {
                return this;
            }
        }

        public void DoIdle() {
        }

        public MockVsTextView CreateTextView(string contentType, string file, string content = "") {
            return Invoke(() => CreateTextViewWorker(contentType, file, content));
        }

        private MockVsTextView CreateTextViewWorker(string contentType, string file, string content) {
            var buffer = new MockTextBuffer(content, ContentTypeRegistry.GetContentType(contentType), file);
            foreach (var classifier in Container.GetExports<IClassifierProvider, IContentTypeMetadata>()) {
                foreach (var targetContentType in classifier.Metadata.ContentTypes) {
                    if (buffer.ContentType.IsOfType(targetContentType)) {
                        classifier.Value.GetClassifier(buffer);
                    }
                }
            }

            var view = new MockTextView(buffer);
            var res = new MockVsTextView(_serviceProvider, this, view);
            view.Properties[typeof(MockVsTextView)] = res;

            // Initialize code window
            LanguageServiceInfo info;
            if (CachedInfo.LangServicesByName.TryGetValue(contentType, out info)) {
                var id = info.Attribute.LanguageServiceSid;
                var serviceProvider = Container.GetExportedValue<MockVsServiceProvider>();
                var langInfo = (IVsLanguageInfo)serviceProvider.GetService(id);
                IVsCodeWindowManager mgr;
                var codeWindow = new MockCodeWindow(serviceProvider, view);
                view.Properties[typeof(MockCodeWindow)] = codeWindow;
                if (ErrorHandler.Succeeded(langInfo.GetCodeWindowManager(codeWindow, out mgr))) {
                    if (ErrorHandler.Failed(mgr.AddAdornments())) {
                        Console.WriteLine("Failed to add adornments to text view");
                    }
                }
            }

            // Initialize intellisense imports
            var providers = Container.GetExports<IIntellisenseControllerProvider, IContentTypeMetadata>();
            foreach (var provider in providers) {
                foreach (var targetContentType in provider.Metadata.ContentTypes) {
                    if (buffer.ContentType.IsOfType(targetContentType)) {
                        provider.Value.TryCreateIntellisenseController(
                            view,
                            new[] { buffer }
                        );
                        break;
                    }
                }
            }

            // tell the world we have a new view...
            foreach (var listener in Container.GetExports<IVsTextViewCreationListener, IContentTypeMetadata>()) {
                foreach (var targetContentType in listener.Metadata.ContentTypes) {
                    if (buffer.ContentType.IsOfType(targetContentType)) {
                        listener.Value.VsTextViewCreated(res);
                    }
                }
            }

            return res;
        }

        public IContentTypeRegistryService ContentTypeRegistry {
            get {
                if (_contentTypeRegistry == null) {
                    _contentTypeRegistry = Container.GetExport<IContentTypeRegistryService>().Value;
                    var contentDefinitions = Container.GetExports<ContentTypeDefinition, IContentTypeDefinitionMetadata>();
                    foreach (var contentDef in contentDefinitions) {
                        _contentTypeRegistry.AddContentType(
                            contentDef.Metadata.Name,
                            contentDef.Metadata.BaseDefinition
                        );
                    }

                }
                return _contentTypeRegistry;
            }
        }

        #region Composition Container Initialization

        private CompositionContainer CreateCompositionContainer() {
            var container = new CompositionContainer(CachedInfo.Catalog);
            container.ComposeExportedValue<MockVs>(this);
            var batch = new CompositionBatch();

            container.Compose(batch);

            return container;
        }

        private static CachedVsInfo CreateCachedVsInfo() {
            var runningLoc = Path.GetDirectoryName(typeof(MockVs).Assembly.Location);
            // we want to pick up all of the MEF exports which are available, but they don't
            // depend upon us.  So if we're just running some tests in the IDE when the deployment
            // happens it won't have the DLLS with the MEF exports.  So we copy them here.
            TestData.Deploy(null, includeTestData: false);

            // load all of the available DLLs that depend upon TestUtilities into our catalog
            List<AssemblyCatalog> catalogs = new List<AssemblyCatalog>();
            List<Type> packageTypes = new List<Type>();
            foreach (var file in Directory.GetFiles(runningLoc, "*.dll")) {
                Assembly asm;
                try {
                    asm = Assembly.Load(Path.GetFileNameWithoutExtension(file));
                } catch {
                    continue;
                }

                Console.WriteLine("Including {0}", file);
                catalogs.Add(new AssemblyCatalog(asm));
                try {
                    foreach (var type in asm.GetTypes()) {
                        if (type.IsDefined(typeof(PackageRegistrationAttribute), false)) {
                            packageTypes.Add(type);
                        }
                    }
                } catch (ReflectionTypeLoadException tlx) {
                    Console.WriteLine(tlx);
                }
            }

            return new CachedVsInfo(
                new AggregateCatalog(catalogs.ToArray()),
                packageTypes
            );
        }

        #endregion

        public ITreeNode WaitForItemRemoved(params string[] path) {
            throw new NotImplementedException();
        }

        ITreeNode IVisualStudioInstance.WaitForItem(params string[] items) {
            var res = WaitForItem(items);
            if (res.IsNull) {
                return null;
            }
            return new MockTreeNode(this, res);
        }

        public ITreeNode FindItem(params string[] items) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an item from solution explorer.
        /// 
        /// First item is the project name, additional items are the name of the displayed caption in
        /// Solution Explorer.
        /// </summary>
        public HierarchyItem WaitForItem(params string[] items) {
            return Invoke(() => WaitForItemWorker(items));
        }

        private HierarchyItem WaitForItemWorker(string[] items) {
            IVsHierarchy hierarchy;
            if (ErrorHandler.Failed(Solution.GetProjectOfUniqueName(items[0], out hierarchy))) {
                return new HierarchyItem();
            }
            if (items.Length == 1) {
                return new HierarchyItem(hierarchy, (uint)VSConstants.VSITEMID.Root);
            }

            var firstItem = items[1];
            var firstHierItem = new HierarchyItem();
            foreach (var item in hierarchy.GetHierarchyItems()) {
                if (item.Caption == firstItem) {
                    firstHierItem = item;
                    break;
                }
            }

            if (firstHierItem.IsNull) {
                return new HierarchyItem();
            }

            for (int i = 2; i < items.Length; i++) {
                bool found = false;
                foreach (var item in firstHierItem.Children) {
                    if (item.Caption == items[i]) {
                        firstHierItem = item;
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    firstHierItem = new HierarchyItem();
                    break;
                }
            }

            return firstHierItem;
        }

        IEditor IVisualStudioInstance.OpenItem(string project, params string[] path) {
            return OpenItem(project, path);
        }

        public MockVsTextView OpenItem(string project, params string[] path) {
            return Invoke(() => OpenItemWorker(project, path));
        }

        private MockVsTextView OpenItemWorker(string project, string[] path) {
            // matching the API of VisualStudioSolution.OpenItem
            string[] temp = new string[path.Length + 1];
            temp[0] = project;
            Array.Copy(path, 0, temp, 1, path.Length);
            var item = WaitForItem(temp);
            if (item.IsNull) {
                return null;
            }

            string languageName;
            if (!CachedInfo._languageNamesByExtension.TryGetValue(Path.GetExtension(item.CanonicalName), out languageName)) {
                languageName = "code";
            }

            var res = CreateTextView(languageName, item.CanonicalName, File.ReadAllText(item.CanonicalName));
            SetFocus(res);

            uint cookie;
            IVsTextLines lines;
            ErrorHandler.ThrowOnFailure(((IVsTextView)res).GetBuffer(out lines));
            IntPtr linesPtr = Marshal.GetIUnknownForObject(lines);
            try {
                ErrorHandler.ThrowOnFailure(
                    _rdt.RegisterAndLockDocument(
                        (uint)_VSRDTFLAGS.RDT_NoLock,
                        item.CanonicalName,
                        item.Hierarchy,
                        item.ItemId,
                        linesPtr,
                        out cookie
                    )
                );
            } finally {
                Marshal.Release(linesPtr);
            }
            return res;
        }

        internal void SetFocus(IFocusable res) {
            Invoke(() =>SetFocusWorker(res));
        }

        private void SetFocusWorker(IFocusable res) {
            if (_focused != null) {
                _focused.LostFocus();
            }
            res.GetFocus();
            _focused = res;
        }

        ComposablePartCatalog IComponentModel.DefaultCatalog {
            get { throw new NotImplementedException(); }
        }

        ICompositionService IComponentModel.DefaultCompositionService {
            get { throw new NotImplementedException(); }
        }

        ExportProvider IComponentModel.DefaultExportProvider {
            get { throw new NotImplementedException(); }
        }

        ComposablePartCatalog IComponentModel.GetCatalog(string catalogName) {
            throw new NotImplementedException();
        }

        IEnumerable<T> IComponentModel.GetExtensions<T>() {
            return Container.GetExportedValues<T>();
        }

        T IComponentModel.GetService<T>() {
            return Container.GetExportedValue<T>();
        }

        public void Dispose() {
            _shutdown = true;
            _uiEvent.Set();
        }

        
        public void Type(Key key) {
            Invoke(() => TypeWorker(key));
        }

        private void TypeWorker(Key key) {
            var cmdTarget = _focused as IOleCommandTarget;
            if (cmdTarget != null) {
                switch (key) {
                    case Key.F2: cmdTarget.Rename(); break;
                    case Key.Enter: cmdTarget.Enter(); break;
                    case Key.Tab: cmdTarget.Tab(); break;
                    default:
                        throw new InvalidOperationException("Unmapped key " + key);
                }
            }
        }

        public void ControlX() {
            Invoke(() => ControlXWorker());
        }

        private void ControlXWorker() {
            var cmdTarget = _focused as IOleCommandTarget;
            if (cmdTarget != null) {
                cmdTarget.Cut();
            }
        }

        public void ControlC() {
            Invoke(() => ControlCWorker());
        }

        private void ControlCWorker() {
            var cmdTarget = _focused as IOleCommandTarget;
            if (cmdTarget != null) {
                cmdTarget.Copy();
            }
        }

        public void Type(string p) {
            Invoke(() => TypeWorker(p));
        }

        private void TypeWorker(string p) {
            var cmdTarget = _focused as IOleCommandTarget;
            if (cmdTarget != null) {
                cmdTarget.Type(p);
            }
        }

        public void ControlV() {
            Invoke(() => ControlVWorker());
        }

        private void ControlVWorker() {
            var cmdTarget = _focused as IOleCommandTarget;
            if (cmdTarget != null) {
                cmdTarget.Paste();
            }
        }

        public void CheckMessageBox(params string[] text) {
            CheckMessageBox(MessageBoxButton.Cancel, text);
        }

        public void CheckMessageBox(MessageBoxButton button, params string[] text) {
            UIShell.CheckMessageBox(button, text);
        }

        public void Sleep(int ms) {
        }


        public void ExecuteCommand(string command) {
            throw new NotImplementedException();
        }

        public string SolutionFilename {
            get {
                return Solution.SolutionFile;
            }
        }

        public string SolutionDirectory {
            get {
                return Path.GetDirectoryName(SolutionFilename);
            }
        }

        public IntPtr WaitForDialog() {
            throw new NotImplementedException();
        }

        public void WaitForDialogDismissed() {
            throw new NotImplementedException();
        }

        public void AssertFileExists(params string[] path) {
            throw new NotImplementedException();
        }

        public void AssertFileDoesntExist(params string[] path) {
            throw new NotImplementedException();
        }

        public void AssertFolderExists(params string[] path) {
            throw new NotImplementedException();
        }

        public void AssertFolderDoesntExist(params string[] path) {
            throw new NotImplementedException();
        }

        public void AssertFileExistsWithContent(string content, params string[] path) {
            throw new NotImplementedException();
        }

        public void CloseActiveWindow(vsSaveChanges save) {
            throw new NotImplementedException();
        }
        public void WaitForOutputWindowText(string name, string containsText, int timeout = 5000) {
            throw new NotImplementedException();
        }

        public IntPtr OpenDialogWithDteExecuteCommand(string commandName, string commandArgs = "") {
            throw new NotImplementedException();
        }

        public void SelectSolutionNode() {
        }

        public Project GetProject(string projectName) {
            throw new NotImplementedException();
        }

        public void SelectProject(Project project) {
            throw new NotImplementedException();
        }

        public IEditor GetDocument(string filename) {
            throw new NotImplementedException();
        }

        public IAddExistingItem AddExistingItem() {
            throw new NotImplementedException();
        }

        public IOverwriteFile WaitForOverwriteFileDialog() {
            throw new NotImplementedException();
        }


        public IAddNewItem AddNewItem() {
            throw new NotImplementedException();
        }

        public void WaitForMode(dbgDebugMode dbgDebugMode) {
            throw new NotImplementedException();
        }

        public List<IVsTaskItem> WaitForErrorListItems(int expectedCount) {
            throw new NotImplementedException();
        }


        public DTE Dte {
            get { throw new NotImplementedException(); }
        }

        public void OnDispose(Action action) {
            
        }
    }
}
