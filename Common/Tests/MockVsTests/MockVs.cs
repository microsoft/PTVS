// Visual Studio Shared Project
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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using TestUtilities;
using TestUtilities.Mocks;
using MefV1 = System.ComponentModel.Composition;
using Thread = System.Threading.Thread;

namespace Microsoft.VisualStudioTools.MockVsTests {
    public sealed class MockVs : IComponentModel, IDisposable, IVisualStudioInstance {
        internal static CachedVsInfo CachedInfo = CreateCachedVsInfo();
        private ExportProvider _container;
        private IContentTypeRegistryService _contentTypeRegistry;
        private Dictionary<Guid, Package> _packages = new Dictionary<Guid, Package>();
        internal MockVsTextManager TextManager;
        internal readonly MockActivityLog ActivityLog = new MockActivityLog();
        internal readonly MockSettingsManager SettingsManager = new MockSettingsManager();
        internal readonly MockLocalRegistry LocalRegistry = new MockLocalRegistry();
        internal readonly MockVsDebugger Debugger = new MockVsDebugger();
        internal readonly MockVsTrackProjectDocuments TrackDocs = new MockVsTrackProjectDocuments();
        internal readonly MockVsShell Shell = new MockVsShell();
        internal MockVsUIShell UIShell;
        public readonly MockVsSolution Solution = new MockVsSolution();
        private MockVsServiceProvider _serviceProvider;
        private readonly List<MockVsTextView> _views = new List<MockVsTextView>();
        private readonly MockVsProfferCommands _proferredCommands = new MockVsProfferCommands();
        private readonly MockOleComponentManager _compManager = new MockOleComponentManager();
        private readonly MockOutputWindow _outputWindow = new MockOutputWindow();
        private readonly MockVsBuildManagerAccessor _buildManager = new MockVsBuildManagerAccessor();
        private readonly MockUIHierWinClipboardHelper _hierClipHelper = new MockUIHierWinClipboardHelper();
        internal MockVsMonitorSelection _monSel;
        internal uint _monSelCookie;
        internal MockVsUIHierarchyWindow _uiHierarchy;
        private readonly MockVsQueryEditQuerySave _queryEditSave = new MockVsQueryEditQuerySave();
        private MockVsRunningDocumentTable _rdt;
        private readonly MockVsUIShellOpenDocument _shellOpenDoc = new MockVsUIShellOpenDocument();
        private readonly MockVsSolutionBuildManager _slnBuildMgr = new MockVsSolutionBuildManager();
        private readonly MockVsExtensibility _extensibility = new MockVsExtensibility();
        private MockDTE _dte;
        private bool _shutdown;
        private AutoResetEvent _uiEvent = new AutoResetEvent(false);
        private readonly List<Action> _uiEvents = new List<Action>();
        private readonly Thread _throwExceptionsOn;
        private ExceptionDispatchInfo _edi;
        private readonly List<IMockPackage> _loadedPackages = new List<IMockPackage>();

        internal IOleCommandTarget
            /*_contextTarget, */    // current context menu
            /*_toolbarTarget, */    // current toolbar
            _focusTarget,       // current IVsWindowFrame that has focus
            _docCmdTarget,      // current document
            _projectTarget/*,   // current IVsHierarchy
            _shellTarget*/;

        private readonly Thread UIThread;

        public MockVs() {
#if DEV15_OR_LATER
            // If we are not in Visual Studio, we need to set MSBUILD_EXE_PATH
            // to use any project support.
            if (!"devenv".Equals(Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location), StringComparison.OrdinalIgnoreCase)) {
                var vsPath = Environment.GetEnvironmentVariable("VisualStudio_" + AssemblyVersionInfo.VSVersion);
                if (!Directory.Exists(vsPath)) {
                    vsPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    if (string.IsNullOrEmpty(vsPath)) {
                        vsPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    }
                    vsPath = Path.Combine(vsPath, "Microsoft Visual Studio", AssemblyVersionInfo.VSVersionSuffix);
                    foreach (var sku in new[] { "Enterprise", "Professional", "Community" }) {
                        if (Directory.Exists(Path.Combine(vsPath, sku))) {
                            vsPath = Path.Combine(vsPath, sku);
                            break;
                        }
                    }
                }
                if (Directory.Exists(vsPath)) {
                    var msbuildPath = Path.Combine(vsPath, "MSBuild");
                    var msbuildExe = FileUtils.EnumerateFiles(msbuildPath, "msbuild.exe").OrderByDescending(k => k).FirstOrDefault();
                    if (File.Exists(msbuildExe)) {
                        // Set the variable. If we haven't set it, most tests
                        // should still work, but ones trying to load MSBuild's
                        // assemblies will fail.
                        Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildExe);
                    }
                }
            }
#endif

            _throwExceptionsOn = Thread.CurrentThread;

            using (var e = new AutoResetEvent(false)) {
                UIThread = new Thread(UIThreadWorker);
                UIThread.SetApartmentState(ApartmentState.STA);
                UIThread.Name = "Mock UI Thread";
                UIThread.Start((object)e);
                // Wait for UI thread to start before returning. This ensures that
                // any packages we have are loaded and have published their services
                e.WaitOne();
            }
            ThrowPendingException();
        }

        public void Dispose() {
            // Dispose of packages while the UI thread is still running, it's
            // possible some packages may need to get back onto the UI thread
            // before their Dispose is complete.  TaskProvider does this - it wants
            // to wait for the task provider thread to exit, but the same thread
            // maybe attempting to get back onto the UI thread.  If we yank out
            // the UI thread first then it never makes it over and we just stop
            // responding and deadlock.
            foreach (var package in _loadedPackages) {
                package.Dispose();
            }

            _monSel.UnadviseSelectionEvents(_monSelCookie);
            Shell.SetProperty((int)__VSSPROPID6.VSSPROPID_ShutdownStarted, true);
            _serviceProvider.Dispose();
            _container.Dispose();
            _shutdown = true;
            _uiEvent.Set();
            if (!UIThread.Join(TimeSpan.FromSeconds(30))) {
                Console.WriteLine("Failed to wait for UI thread to terminate");
            }
            ThrowPendingException();
            AssertListener.ThrowUnhandled();
        }


        class SelectionEvents : IVsSelectionEvents {
            private readonly MockVs _vs;

            public SelectionEvents(MockVs vs) {
                _vs = vs;
            }

            public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) {
                return VSConstants.S_OK;
            }

            public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew) {
                return VSConstants.S_OK;
            }

            public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew) {
                _vs._projectTarget = pHierNew as IOleCommandTarget;
                _vs._focusTarget = pSCNew as IOleCommandTarget;
                return VSConstants.S_OK;
            }
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

        public void AssertUIThread() {
            Assert.AreEqual(UIThread, Thread.CurrentThread);
        }

        private void UIThreadWorker(object evt) {
            Console.WriteLine($"Started UIThreadWorker on {Thread.CurrentThread.ManagedThreadId}");
            try {
                try {
                    SynchronizationContext.SetSynchronizationContext(new MockSyncContext(this));

                    TextManager = new MockVsTextManager(this);
                    _container = CreateCompositionContainer();

                    _serviceProvider = _container.GetExportedValue<MockVsServiceProvider>();
                    UIShell = new MockVsUIShell(this);
                    _monSel = new MockVsMonitorSelection(this);
                    _uiHierarchy = new MockVsUIHierarchyWindow(this);
                    _rdt = new MockVsRunningDocumentTable(this);
                    _dte = new MockDTE(this);
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
                    _serviceProvider.AddService(typeof(EnvDTE.DTE), _dte);

                    Shell.SetProperty((int)__VSSPROPID4.VSSPROPID_ShellInitialized, true);

                    UIShell.AddToolWindow(new Guid(ToolWindowGuids80.SolutionExplorer), new MockToolWindow(_uiHierarchy));

                    ErrorHandler.ThrowOnFailure(
                        _monSel.AdviseSelectionEvents(
                            new SelectionEvents(this),
                            out _monSelCookie
                        )
                    );

                    foreach (var package in _container.GetExportedValues<IMockPackage>()) {
                        _loadedPackages.Add(package);
                        package.Initialize();
                    }
                } finally {
                    ((AutoResetEvent)evt).Set();
                }
                RunMessageLoop();
            } catch (Exception ex) {
                Trace.TraceError("Captured exception on mock UI thread: {0}", ex);
                _edi = ExceptionDispatchInfo.Capture(ex);
            }
        }

        internal void RunMessageLoop(AutoResetEvent dialogEvent = null) {
            WaitHandle[] handles;
            if (dialogEvent == null) {
                handles = new[] { _uiEvent };
            } else {
                handles = new[] { _uiEvent, dialogEvent };
            }

            while (!_shutdown) {
                if (WaitHandle.WaitAny(handles) == 1) {
                    // dialog is closing...
                    break;
                }
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

        public bool HasPendingException => _edi != null;

        public void ThrowPendingException() => ThrowPendingException(false);

        private void ThrowPendingException(bool checkThread) {
            if (!checkThread || _throwExceptionsOn == Thread.CurrentThread) {
                var edi = Interlocked.Exchange(ref _edi, null);
                if (edi != null) {
                    edi.Throw();
                }
            }
        }

        public void Invoke(Action action) {
            ThrowPendingException();
            if (Thread.CurrentThread == UIThread) {
                action();
                return;
            }
            lock (_uiEvents) {
                _uiEvents.Add(action);
                _uiEvent.Set();
            }
        }

        public void InvokeSync(Action action, int timeout = 30000) {
            Invoke(() => {
                action();
                return 0;
            });
        }

        public T InvokeTask<T>(Func<Task<T>> taskCreator, int timeout = 30000) {
            return WaitForTask(Invoke(taskCreator, timeout), timeout);
        }

        public T Invoke<T>(Func<T> func, int timeout = 30000) {
            ThrowPendingException();
            if (Thread.CurrentThread == UIThread) {
                return func();
            }

            var tcs = new TaskCompletionSource<T>();
            Action action = () => {
                try {
                    tcs.SetResult(func());
                } catch (Exception ex) {
                    tcs.SetException(ex);
                }
            };

            lock (_uiEvents) {
                _uiEvents.Add(action);
                _uiEvent.Set();
            }

            return WaitForTask(tcs.Task, timeout);
        }

        private T WaitForTask<T>(Task<T> task, int timeout) {
            try {
                if (timeout > 0) {
                    if (!task.Wait(timeout)) {
                        Assert.Fail("Timed out waiting for operation");
                        throw new OperationCanceledException();
                    }
                } else {
                    while (!task.Wait(100)) {
                        if (!UIThread.IsAlive) {
                            ThrowPendingException(checkThread: false);
                            Debug.Fail("UIThread was terminated");
                            return default(T);
                        }
                    }
                }
            } catch (AggregateException ae) when (ae.InnerException != null) {
                throw ae.InnerException;
            }
            ThrowPendingException(checkThread: false);
            return task.Result;
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

        public MockVsTextView CreateTextView(
            string contentType,
            string content,
            Action<MockVsTextView> onCreate = null,
            string file = null
        ) {
            return Invoke(() => CreateTextViewWorker(contentType, content, onCreate, file));
        }

        private MockVsTextView CreateTextViewWorker(
            string contentType,
            string content,
            Action<MockVsTextView> onCreate,
            string file = null
        ) {
            var buffer = new MockTextBuffer(content, ContentTypeRegistry.GetContentType(contentType), file);

            var view = new MockTextView(buffer);
            var res = new MockVsTextView(_serviceProvider, this, view);
            view.Properties[typeof(MockVsTextView)] = res;
            onCreate?.Invoke(res);

            var classifier = res.Classifier;
            if (classifier != null) {
                classifier.GetClassificationSpans(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length));
            }

            // Initialize code window
            LanguageServiceInfo info;
            if (CachedInfo.LangServicesByName.TryGetValue(contentType, out info)) {
                var id = info.Attribute.LanguageServiceSid;
                var serviceProvider = _container.GetExportedValue<MockVsServiceProvider>();
                var langInfo = (IVsLanguageInfo)serviceProvider.GetService(id);
                if (langInfo == null) {
                    throw new NotImplementedException("Unable to get IVsLanguageInfo for " + info.Attribute.LanguageName);
                }
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
            var providers = _container.GetExports<IIntellisenseControllerProvider, IContentTypeMetadata>();
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
            foreach (var listener in _container.GetExports<IVsTextViewCreationListener, IContentTypeMetadata>()) {
                foreach (var targetContentType in listener.Metadata.ContentTypes) {
                    if (buffer.ContentType.IsOfType(targetContentType)) {
                        listener.Value.VsTextViewCreated(res);
                    }
                }
            }

            OnDispose(() => res.Close());
            return res;
        }

        public IContentTypeRegistryService ContentTypeRegistry {
            get {
                if (_contentTypeRegistry == null) {
                    _contentTypeRegistry = _container.GetExport<IContentTypeRegistryService>().Value;
                    var contentDefinitions = _container.GetExports<ContentTypeDefinition, IContentTypeDefinitionMetadata>();
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

        private ExportProvider CreateCompositionContainer() {
            var catalog = CachedInfo.Catalog.AddInstance(() => this);

            var configuration = CompositionConfiguration.Create(catalog);
            var runtimeConfiguration = RuntimeComposition.CreateRuntimeComposition(configuration);
            var exportProviderFactory = runtimeConfiguration.CreateExportProviderFactory();
            return exportProviderFactory.CreateExportProvider();
        }

        private static CachedVsInfo CreateCachedVsInfo() {
            var runningLoc = Path.GetDirectoryName(typeof(MockVs).Assembly.Location);

            // load all of the available DLLs that depend upon TestUtilities into our catalog
            var assemblies = new List<Assembly>();
            var packageTypes = new List<Type>();

            var excludedAssemblies = new HashSet<string>(new string[] {
                "Microsoft.VisualStudio.Text.Internal.dll",
                "Microsoft.VisualStudio.Utilities.dll",
                "Microsoft.VisualStudio.Validation.dll",
                "Microsoft.VisualStudio.Workspace.dll",
                "Microsoft.VisualStudio.Debugger.DebugAdapterHost.Interfaces.dll",
                "TestUtilities"
            }, StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(runningLoc, "*.dll")) {
                if (excludedAssemblies.Contains(Path.GetFileName(file))) {
                    continue;
                }

                Assembly asm;
                try {
                    asm = Assembly.Load(Path.GetFileNameWithoutExtension(file));
                } catch {
                    continue;
                }

                Console.WriteLine("Including {0}", file);
                try {
                    foreach (var type in asm.GetTypes()) {
                        if (type.IsDefined(typeof(PackageRegistrationAttribute), false)) {
                            packageTypes.Add(type);
                        }
                    }
                    assemblies.Add(asm);
                } catch (TypeInitializationException tix) {
                    Console.WriteLine(tix);
                } catch (ReflectionTypeLoadException tlx) {
                    Console.WriteLine(tlx);
                    foreach (var ex in tlx.LoaderExceptions) {
                        Console.WriteLine(ex);
                    }
                } catch (IOException iox) {
                    Console.WriteLine(iox);
                }
            }

            var catalog = MefCatalogFactory.CreateAssembliesCatalog(
                    "Microsoft.VisualStudio.CoreUtility",
                    "Microsoft.VisualStudio.Text.Data",
                    "Microsoft.VisualStudio.Text.Logic",
                    "Microsoft.VisualStudio.Text.UI",
                    "Microsoft.VisualStudio.Text.UI.Wpf",
                    "Microsoft.VisualStudio.InteractiveWindow",
                    "Microsoft.VisualStudio.VsInteractiveWindow",
                    "Microsoft.VisualStudio.Editor",
                    "Microsoft.VisualStudio.Language.Intellisense",
                    "Microsoft.PythonTools",
                    "Microsoft.PythonTools.TestAdapter",
                    "Microsoft.PythonTools.VSInterpreters",
                    "MockVsTests",
                    "PythonToolsMockTests")
                .WithCompositionService()
                .AddType<MockTextUndoHistoryRegistry>()
                .AddType<MockContentTypeRegistryService>()
                .AddType<MockClassificationTypeRegistryService>();

            return new CachedVsInfo(catalog, packageTypes);
        }
        #endregion

        public ITreeNode WaitForItemRemoved(params string[] path) {
            ITreeNode item = null;
            for (int i = 0; i < 400; i++) {
                item = FindItem(path);
                if (item == null) {
                    break;
                }
                Thread.Sleep(25);
            }
            return item;
        }

        ITreeNode IVisualStudioInstance.WaitForItem(params string[] items) {
            var res = WaitForItem(items);
            if (res.IsNull) {
                return null;
            }
            return new MockTreeNode(this, res);
        }

        public ITreeNode FindItem(params string[] items) {
            var res = WaitForItem(items);
            if (res.IsNull) {
                return null;
            }
            return new MockTreeNode(this, res);
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
            var item = WaitForItemWorker(temp);
            if (item.IsNull) {
                return null;
            }

            string languageName;
            if (!CachedInfo._languageNamesByExtension.TryGetValue(Path.GetExtension(item.CanonicalName), out languageName)) {
                languageName = "code";
            }

            var res = CreateTextViewWorker(languageName, File.ReadAllText(item.CanonicalName), view => {
                uint cookie;
                IVsTextLines lines;
                ErrorHandler.ThrowOnFailure(((IVsTextView)view).GetBuffer(out lines));
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
            }, item.CanonicalName);
            if (_docCmdTarget != null) {
                ((IFocusable)_docCmdTarget).LostFocus();
            }
            _docCmdTarget = res;
            ((IFocusable)res).GetFocus();

            return res;
        }

        ComposablePartCatalog IComponentModel.DefaultCatalog {
            get { throw new NotImplementedException(); }
        }

        MefV1.ICompositionService IComponentModel.DefaultCompositionService {
            get { throw new NotImplementedException(); }
        }

        MefV1.Hosting.ExportProvider IComponentModel.DefaultExportProvider {
            get { return _container.AsExportProvider(); }
        }

        ComposablePartCatalog IComponentModel.GetCatalog(string catalogName) {
            throw new NotImplementedException();
        }

        IEnumerable<T> IComponentModel.GetExtensions<T>() {
            return _container.GetExportedValues<T>();
        }

        T IComponentModel.GetService<T>() {
            return _container.GetExportedValue<T>();
        }


        public void Type(Key key) {
            Invoke(() => TypeWorker(key));
        }

        private void TypeWorker(Key key) {
            Guid guid;
            switch (key) {
                case Key.F2:
                    guid = VSConstants.GUID_VSStandardCommandSet97;
                    Exec(ref guid, (int)VSConstants.VSStd97CmdID.Rename, 0, IntPtr.Zero, IntPtr.Zero);
                    break;
                case Key.Enter:
                    guid = VSConstants.VSStd2K;
                    Exec(ref guid, (int)VSConstants.VSStd2KCmdID.RETURN, 0, IntPtr.Zero, IntPtr.Zero);
                    break;
                case Key.Tab:
                    guid = VSConstants.VSStd2K;
                    Exec(ref guid, (int)VSConstants.VSStd2KCmdID.TAB, 0, IntPtr.Zero, IntPtr.Zero);
                    break;
                case Key.Delete:
                    guid = VSConstants.VSStd2K;
                    Exec(ref guid, (int)VSConstants.VSStd2KCmdID.DELETE, 0, IntPtr.Zero, IntPtr.Zero);
                    break;
                default:
                    throw new InvalidOperationException("Unmapped key " + key);
            }
        }


        private IEnumerable<IOleCommandTarget> Targets {
            get {
                if (_focusTarget != null) {
                    yield return _focusTarget;
                }
                if (_docCmdTarget != null) {
                    yield return _docCmdTarget;
                }
                if (_projectTarget != null) {
                    yield return _projectTarget;
                }
            }
        }

        private int Exec(ref Guid cmdGroup, uint cmdId, uint cmdExecopt, IntPtr pvaIn, IntPtr pvaOut) {
            int hr = NativeMethods.OLECMDERR_E_NOTSUPPORTED;
            foreach (var target in Targets) {
                hr = target.Exec(
                    cmdGroup,
                    cmdId,
                    cmdExecopt,
                    pvaIn,
                    pvaOut
                );
                if (hr == NativeMethods.OLECMDERR_E_CANCELED ||
                    (hr != NativeMethods.OLECMDERR_E_NOTSUPPORTED &&
                    hr != NativeMethods.OLECMDERR_E_UNKNOWNGROUP)) {
                    if (hr == NativeMethods.OLECMDERR_E_CANCELED) {
                        hr = VSConstants.S_OK;
                    }
                    break;
                }
            }
            return hr;
        }

        private void ContinueRouting() {
        }

        public void ControlX() {
            Invoke(() => ControlXWorker());
        }

        private void ControlXWorker() {
            var guid = VSConstants.GUID_VSStandardCommandSet97;
            Exec(ref guid, (int)VSConstants.VSStd97CmdID.Cut, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public void ControlC() {
            Invoke(() => ControlCWorker());
        }

        private void ControlCWorker() {
            var guid = VSConstants.GUID_VSStandardCommandSet97;
            Exec(ref guid, (int)VSConstants.VSStd97CmdID.Copy, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public void Type(string p) {
            Invoke(() => TypeWorker(p));
        }

        public void PressAndRelease(Key key, params Key[] modifier) {
            throw new NotImplementedException();
        }

        private void TypeWorker(string p) {
            if (UIShell.Dialogs.Count != 0) {
                UIShell.Dialogs.Last().Type(p);
                return;
            }

            TypeCmdTarget(p);
        }

        private void TypeCmdTarget(string text) {
            var guid = VSConstants.VSStd2K;
            var variantMem = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VARIANT)));
            try {
                for (int i = 0; i < text.Length; i++) {
                    switch (text[i]) {
                        case '\r': TypeWorker(Key.Enter); break;
                        case '\t': TypeWorker(Key.Tab); break;
                        default:
                            Marshal.GetNativeVariantForObject((ushort)text[i], variantMem);
                            Exec(
                                ref guid,
                                (int)VSConstants.VSStd2KCmdID.TYPECHAR,
                                0,
                                variantMem,
                                IntPtr.Zero
                            );
                            break;
                    }

                }
            } finally {
                Marshal.FreeCoTaskMem(variantMem);
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct VARIANT {
            [FieldOffset(0)]
            public ushort vt;
            [FieldOffset(8)]
            public IntPtr pdispVal;
            [FieldOffset(8)]
            public byte ui1;
            [FieldOffset(8)]
            public ushort ui2;
            [FieldOffset(8)]
            public uint ui4;
            [FieldOffset(8)]
            public ulong ui8;
            [FieldOffset(8)]
            public float r4;
            [FieldOffset(8)]
            public double r8;
        }

        public void ControlV() {
            Invoke(() => ControlVWorker());
        }

        private void ControlVWorker() {
            var guid = VSConstants.GUID_VSStandardCommandSet97;
            Exec(ref guid, (int)VSConstants.VSStd97CmdID.Paste, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public void CheckMessageBox(params string[] text) {
            CheckMessageBox(MessageBoxButton.Cancel, text);
        }

        public void CheckMessageBox(MessageBoxButton button, params string[] text) {
            UIShell.CheckMessageBox(button, text);
        }

        public void MaybeCheckMessageBox(MessageBoxButton button, params string[] text) {
            UIShell.MaybeCheckMessageBox(button, text);
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
            return UIShell.WaitForDialog();
        }

        public void WaitForDialogDismissed() {
            UIShell.WaitForDialogDismissed();
        }

        public void AssertFileExists(params string[] path) {
            Assert.IsNotNull(FindItem(path));

            var basePath = Path.Combine(new[] { SolutionDirectory }.Concat(path).ToArray());
            Assert.IsTrue(File.Exists(basePath), "File doesn't exist: " + basePath);
        }

        public void AssertFileDoesntExist(params string[] path) {
            Assert.IsNull(FindItem(path));

            var basePath = Path.Combine(new[] { SolutionDirectory }.Concat(path).ToArray());
            Assert.IsFalse(File.Exists(basePath), "File exists: " + basePath);
        }

        public void AssertFolderExists(params string[] path) {
            Assert.IsNotNull(FindItem(path));

            var basePath = Path.Combine(new[] { SolutionDirectory }.Concat(path).ToArray());
            Assert.IsTrue(Directory.Exists(basePath), "Folder doesn't exist: " + basePath);
        }

        public void AssertFolderDoesntExist(params string[] path) {
            Assert.IsNull(FindItem(path));

            var basePath = Path.Combine(new[] { SolutionDirectory }.Concat(path).ToArray());
            Assert.IsFalse(Directory.Exists(basePath), "Folder exists: " + basePath);
        }

        public void AssertFileExistsWithContent(string content, params string[] path) {
            Assert.IsNotNull(FindItem(path));

            var basePath = Path.Combine(new[] { SolutionDirectory }.Concat(path).ToArray());
            Assert.IsTrue(File.Exists(basePath), "File doesn't exist: " + basePath);
            Assert.AreEqual(File.ReadAllText(basePath), content);
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
            var empty = Guid.Empty;
            IEnumHierarchies ppenum;
            ErrorHandler.ThrowOnFailure(Solution.GetProjectEnum(
                (uint)__VSENUMPROJFLAGS.EPF_ALLPROJECTS,
                ref empty,
                out ppenum
            ));

            var projects = new IVsHierarchy[32];
            int hr;
            uint fetched;
            while ((hr = ppenum.Next((uint)projects.Length, projects, out fetched)) == VSConstants.S_OK && fetched > 0) {
                var project = projects.FirstOrDefault(h =>
                    projectName.Equals(h.GetPropertyValue((int)__VSHPROPID.VSHPROPID_Name, (uint)VSConstants.VSITEMID.Root) as string)
                );
                if (project != null) {
                    return project.GetPropertyValue((int)__VSHPROPID.VSHPROPID_ExtObject, (uint)VSConstants.VSITEMID.Root) as Project;
                }
            }

            return null;
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
            get { return _dte; }
        }

        public void OnDispose(Action action) {

        }
    }
}
