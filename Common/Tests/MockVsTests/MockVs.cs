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
    public class MockVs : IComponentModel {
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
        public readonly MockVsSolution Solution = new MockVsSolution();
        private readonly MockVsServiceProvider _serviceProvider;
        private readonly List<MockVsTextView> _views = new List<MockVsTextView>();
        private readonly MockOleComponentManager _compManager = new MockOleComponentManager();
        private IFocusable _focused;

        public MockVs() {
            TextManager = new MockVsTextManager(this);
            Container = CreateCompositionContainer();
            var serviceProvider = _serviceProvider = Container.GetExportedValue<MockVsServiceProvider>();
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

            // We could do this, but people can cache results of it...
            //((IObjectWithSite)ServiceProvider.GlobalProvider).SetSite(serviceProvider);

            foreach (var package in Container.GetExportedValues<IMockPackage>()) {
                package.Initialize();
            }

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
            var buffer = new MockTextBuffer(content, ContentTypeRegistry.GetContentType(contentType), file);
            foreach (var classifier in Container.GetExports<IClassifierProvider, IContentTypeMetadata>()) {
                foreach (var targetContentType in classifier.Metadata.ContentTypes) {
                    if (buffer.ContentType.IsOfType(targetContentType)) {
                        classifier.Value.GetClassifier(buffer);
                    }
                }
            }

            var view = new MockTextView(buffer);
            var res = new MockVsTextView(_serviceProvider, view);
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
                    /*ErrorHandler.ThrowOnFailure(*/mgr.AddAdornments()/*)*/;
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

                foreach (var type in asm.GetTypes()) {
                    if (type.IsDefined(typeof(PackageRegistrationAttribute), false)) {
                        packageTypes.Add(type);
                    }
                }
            }

            return new CachedVsInfo(
                new AggregateCatalog(catalogs.ToArray()),
                packageTypes
            );
        }

        #endregion

        /// <summary>
        /// Gets an item from solution explorer.
        /// 
        /// First item is the project name, additional items are the name of the displayed caption in
        /// Solution Explorer.
        /// </summary>
        public HierarchyItem WaitForItem(params string[] items) {
            IVsHierarchy hierarchy;
            if (ErrorHandler.Failed(Solution.GetProjectOfUniqueName(items[0], out hierarchy))) {
                return new HierarchyItem();
            }

            var firstItem = items[1];
            var firstHierItem = new HierarchyItem();;
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

        public MockVsTextView OpenItem(string project, params string[] path) {
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
            if (_focused != null) {
                _focused.LostFocus();
            }
            res.GetFocus();
            _focused = res;
            return res;
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
    }
}
