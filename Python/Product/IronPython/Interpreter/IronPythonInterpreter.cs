// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Interpreter.LegacyDB;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreter :
        IPythonInterpreter,
        IDotNetPythonInterpreter,
        IPythonInterpreterWithProjectReferences,
        IDisposable
    {
        private readonly Dictionary<ObjectIdentityHandle, IMember> _members = new Dictionary<ObjectIdentityHandle, IMember>();
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly ConcurrentBag<string> _assemblyLoadSet = new ConcurrentBag<string>();
        private readonly HashSet<ProjectReference> _projectReferenceSet = new HashSet<ProjectReference>();
        private RemoteInterpreterProxy _remote;
        private DomainUnloader _unloader;
        private PythonAnalyzer _state;
        private readonly IPythonInterpreterFactory _factory;
        private readonly IronPythonBuiltinModule _builtinModule;
        private PythonTypeDatabase _typeDb;
#if DEBUG
        private int _id;
        private static int _interpreterCount;
#endif

        public IronPythonInterpreter(IPythonInterpreterFactory factory) {
#if DEBUG
            _id = Interlocked.Increment(ref _interpreterCount);
            Debug.WriteLine(String.Format("IronPython Interpreter {0} created from {1}", _id, factory.GetType().FullName));
            try {
                Debug.WriteLine(new StackTrace(true).ToString());
            } catch (System.Security.SecurityException) {
            }
#endif

            _factory = factory;

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver.Instance.CurrentDomain_AssemblyResolve;

            InitializeRemoteDomain();

            try {
                LoadAssemblies();
            } catch {
                // IronPython not installed in the GAC...
            }

            var mod = Remote.ImportBuiltinModule("__builtin__");
            var newMod = new IronPythonBuiltinModule(this, mod, "__builtin__");
            _modules[newMod.Name] = _builtinModule = newMod;

            if (_factory is PythonInterpreterFactoryWithDatabase withDb) {
                _typeDb = withDb.GetCurrentDatabase().CloneWithNewBuiltins(newMod);
                withDb.NewDatabaseAvailable += OnNewDatabaseAvailable;
            }

            LoadModules();
        }

        void OnNewDatabaseAvailable(object sender, EventArgs e) {
            if (_factory is PythonInterpreterFactoryWithDatabase withDb) {
                var mod = _modules["__builtin__"] as IBuiltinPythonModule;
                _typeDb = withDb.GetCurrentDatabase().CloneWithNewBuiltins(mod);
                RaiseModuleNamesChanged();
            }
        }

        private void InitializeRemoteDomain() {
            var remoteDomain = CreateDomain(out _remote);
            _unloader = new DomainUnloader(remoteDomain);
        }

        private AppDomain CreateDomain(out RemoteInterpreterProxy remoteInterpreter) {
            // We create a sacrificial domain for loading all of our assemblies into.  

            AppDomainSetup setup = new AppDomainSetup();
            setup.ShadowCopyFiles = "true";
            // We are in ...\Extensions\Microsoft\IronPython Interpreter\2.0
            // We need to be able to load assemblies from:
            //      Python Tools for Visual Studio\2.0
            //      IronPython Interpreter\2.0
            //
            // So setup the application base to be Extensions\Microsoft\, and then add the other 2 dirs to the private bin path.
            setup.ApplicationBase = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));
            setup.PrivateBinPath = Path.GetDirectoryName(typeof(IronPythonInterpreter).Assembly.Location) + ";" +
                                   Path.GetDirectoryName(typeof(IPythonFunction).Assembly.Location);

            setup.PrivateBinPathProbe = "";
            if (Directory.Exists(_factory.Configuration.PrefixPath)) {
                setup.AppDomainInitializer = IronPythonResolver.Initialize;
                setup.AppDomainInitializerArguments = new[] { _factory.Configuration.PrefixPath };
            }
            var domain = AppDomain.CreateDomain("IronPythonAnalysisDomain", null, setup);

            remoteInterpreter = (RemoteInterpreterProxy)domain.CreateInstanceAndUnwrap(
                typeof(RemoteInterpreterProxy).Assembly.FullName,
                typeof(RemoteInterpreterProxy).FullName);

#if DEBUG
            var assertListener = Debug.Listeners["Microsoft.PythonTools.AssertListener"];
            if (assertListener != null) {
                var init = (AssertListenerInitializer)domain.CreateInstanceAndUnwrap(
                    typeof(AssertListenerInitializer).Assembly.FullName,
                    typeof(AssertListenerInitializer).FullName
                );
                init.Initialize(assertListener);
            }
#endif

            return domain;
        }

#if DEBUG
        class AssertListenerInitializer : MarshalByRefObject {
            public AssertListenerInitializer() { }

            public void Initialize(TraceListener listener) {
                if (Debug.Listeners[listener.Name] == null) {
                    Debug.Listeners.Add(listener);
                    Debug.Listeners.Remove("Default");
                }
            }
        }
#endif

        class AssemblyResolver {
            internal static AssemblyResolver Instance = new AssemblyResolver();

            public Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
                if (new AssemblyName(args.Name).FullName == typeof(RemoteInterpreterProxy).Assembly.FullName) {
                    return typeof(RemoteInterpreterProxy).Assembly;
                }
                return null;
            }
        }

        public RemoteInterpreterProxy Remote {
            get {
                return _remote;
            }
        }

        private void LoadModules() {
            if (!string.IsNullOrEmpty(_factory.Configuration.PrefixPath)) {
                var dlls = PathUtils.GetAbsoluteDirectoryPath(_factory.Configuration.PrefixPath, "DLLs");
                if (Directory.Exists(dlls)) {
                    foreach (var dll in PathUtils.EnumerateFiles(dlls, "*.dll", recurse: false)) {
                        try {
                            var assem = Remote.LoadAssemblyFromFileWithPath(dll);
                            if (assem != null) {
                                Remote.AddAssembly(assem);
                            }
                        } catch (Exception ex) {
                            Debug.Fail(ex.ToString());
                        }
                    }
                }
            }

            foreach (string modName in Remote.GetBuiltinModuleNames()) {
                try {
                    var mod = Remote.ImportBuiltinModule(modName);

                    if (modName != "__builtin__") {
                        _modules[modName] = new IronPythonModule(this, mod, modName);
                    }
                } catch {
                    // importing can throw, ignore that module
                    continue;
                }
            }
        }

        public void Initialize(PythonAnalyzer state) {
            if (_state != null) {
                _state.SearchPathsChanged -= PythonAnalyzer_SearchPathsChanged;
            }

            _state = state;
            SpecializeClrFunctions();

            if (_state != null) {
                _state.SearchPathsChanged += PythonAnalyzer_SearchPathsChanged;
                PythonAnalyzer_SearchPathsChanged(_state, EventArgs.Empty);
            }
        }

        private void SpecializeClrFunctions() {
            _state.SpecializeFunction("clr", "AddReference", (n, u, p, kw) => AddReference(n, null), true);
            _state.SpecializeFunction("clr", "AddReferenceByPartialName", (n, u, p, kw) => AddReference(n, LoadAssemblyByPartialName), true);
            _state.SpecializeFunction("clr", "AddReferenceByName", (n, u, p, kw) => AddReference(n, null), true);
            _state.SpecializeFunction("clr", "AddReferenceToFile", (n, u, p, kw) => AddReference(n, LoadAssemblyFromFile), true);
            _state.SpecializeFunction("clr", "AddReferenceToFileAndPath", (n, u, p, kw) => AddReference(n, LoadAssemblyFromFileWithPath), true);
        }

        private void PythonAnalyzer_SearchPathsChanged(object sender, EventArgs e) {
            switch (_remote.SetAnalysisDirectories(_state.AnalysisDirectories.ToArray())) {
                case SetAnalysisDirectoriesResult.NoChange:
                    break;
                case SetAnalysisDirectoriesResult.ModulesChanged:
                    ClearAssemblyLoadSet();
                    RaiseModuleNamesChanged();
                    break;
                case SetAnalysisDirectoriesResult.Reload:
                    // we are unloading an assembly so we need to create a new app domain because the CLR will continue
                    // to return the assemblies that we've already loaded
                    ReloadRemoteDomain();
                    break;
            }
        }

        private void ClearAssemblyLoadSet() {
            string asm;
            while (_assemblyLoadSet.TryTake(out asm)) {
            }
        }

        private void ReloadRemoteDomain() {
            var oldUnloader = _unloader;

            var evt = UnloadingDomain;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }

            lock (this) {
                _members.Clear();
                _modules.Clear();
                ClearAssemblyLoadSet();

                InitializeRemoteDomain();

                LoadModules();
            }

            RaiseModuleNamesChanged();

            oldUnloader.Dispose();
        }

        public event EventHandler UnloadingDomain;

        private ObjectHandle LoadAssemblyByName(string name) {
            return Remote.LoadAssemblyByName(name);
        }

        private ObjectHandle LoadAssemblyByPartialName(string name) {
            return Remote.LoadAssemblyByPartialName(name);
        }

        private ObjectHandle LoadAssemblyFromFile(string name) {
            return Remote.LoadAssemblyFromFile(name);
        }

        private ObjectHandle LoadAssemblyFromFileWithPath(string name) {
            return Remote.LoadAssemblyFromFileWithPath(name);
        }

        /// <summary>
        /// VS seems to load extensions via Assembly.LoadFrom. When an assembly is being loaded via Assembly.Load the CLR fusion probes privatePath 
        /// set in App.config (devenv.exe.config) first and then tries the code base of the assembly that called Assembly.Load if it was itself loaded via LoadFrom. 
        /// In order to locate IronPython.Modules correctly, the call to Assembly.Load must originate from an assembly in IronPythonTools installation folder. 
        /// Although Microsoft.Scripting is also in that folder it can be loaded first by IronRuby and that causes the Assembly.Load to search in IronRuby's 
        /// installation folder. Adding a reference to IronPython.Modules also makes sure that the assembly is loaded from the same location as IronPythonToolsCore.
        /// </summary>
        private static void LoadAssemblies() {
            GC.KeepAlive(typeof(IronPython.Modules.ArrayModule)); // IronPython.Modules
        }

        private static bool IronPythonExistsIn(string/*!*/ dir) {
            return File.Exists(Path.Combine(dir, "ipy.exe"));
        }

        private IAnalysisSet AddReference(Node node, Func<string, ObjectHandle> partialLoader) {
            // processes a call to clr.AddReference updating project state
            // so that it contains the newly loaded assembly.
            var callExpr = node as CallExpression;
            if (callExpr == null) {
                return AnalysisSet.Empty;
            }
            foreach (var arg in callExpr.Args) {
                var cexpr = arg.Expression as Microsoft.PythonTools.Parsing.Ast.ConstantExpression;
                if (cexpr == null || !(cexpr.Value is string || cexpr.Value is AsciiString)) {
                    // can't process this add reference
                    continue;
                }

                // TODO: Should we do a .NET reflection only load rather than
                // relying on the CLR module here?  That would prevent any code from
                // running although at least we don't taint our own modules which
                // are loaded with this current code.
                var asmName = cexpr.Value as string;
                if (asmName == null) {
                    // check for byte string
                    var bytes = cexpr.Value as AsciiString;
                    if (bytes != null) {
                        asmName = bytes.String;
                    }
                }
                if (asmName != null && !_assemblyLoadSet.Contains(asmName)) {
                    _assemblyLoadSet.Add(asmName);
                    ObjectHandle asm = null;
                    try {
                        if (partialLoader != null) {
                            asm = partialLoader(asmName);
                        } else {
                            try {
                                asm = LoadAssemblyByName(asmName);
                            } catch {
                                asm = null;
                            }
                            if (asm == null) {
                                asm = LoadAssemblyByPartialName(asmName);
                            }
                        }

                        if (asm == null && _state != null) {
                            foreach (var dir in _state.AnalysisDirectories) {
                                if (!PathUtils.IsValidPath(dir) && !PathUtils.IsValidPath(asmName)) {
                                    string path = Path.Combine(dir, asmName);
                                    if (File.Exists(path)) {
                                        asm = Remote.LoadAssemblyFrom(path);
                                    } else if (File.Exists(path + ".dll")) {
                                        asm = Remote.LoadAssemblyFrom(path + ".dll");
                                    } else if (File.Exists(path + ".exe")) {
                                        asm = Remote.LoadAssemblyFrom(path + ".exe");
                                    }
                                }

                            }
                        }
                    } catch {
                    }
                    if (asm != null && Remote.AddAssembly(asm)) {
                        RaiseModuleNamesChanged();
                    }
                }
            }
            return AnalysisSet.Empty;
        }

        internal void RaiseModuleNamesChanged() {
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        #region IPythonInterpreter Members

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            var res = GetTypeFromType(Remote.GetBuiltinType(id));
            if (res == null) {
                throw new KeyNotFoundException(string.Format("{0} ({1})", id, (int)id));
            }
            return res;
        }

        public IList<string> GetModuleNames() {
            List<string> res = new List<string>(_modules.Keys);

            res.AddRange(Remote.GetModuleNames());

            if (_typeDb != null) {
                foreach (var name in _typeDb.GetModuleNames()) {
                    res.Add(name);
                }
            }

            return res;
        }


        public event EventHandler ModuleNamesChanged;

        public IPythonModule GetModule(string name) {
            return _modules[name];
        }

        public IPythonModule ImportModule(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return null;
            }

            if (name == _builtinModule?.Name) {
                return _builtinModule;
            }

            IPythonModule mod;

            if (_modules.TryGetValue(name, out mod)) {
                return mod;
            }

            var handle = Remote.LookupNamespace(name);
            if (!handle.IsNull) {
                mod = MakeObject(handle) as IPythonModule;
                if (mod != null) {
                    return _modules.GetOrAdd(name, mod);
                }
            }

            if (_typeDb != null) {
                var res = _typeDb.GetModule(name);
                if (res != null) {
                    return res;
                }
            } else if (_factory is AstPythonInterpreterFactory apf) {
                var ctxt = new AstPythonInterpreterFactory.TryImportModuleContext {
                    Interpreter = this,
                    BuiltinModule = _builtinModule,
                    ModuleCache = _modules
                };
                for (int retries = 5; retries > 0; --retries) {
                    switch (apf.TryImportModule(name, out mod, ctxt)) {
                        case AstPythonInterpreterFactory.TryImportModuleResult.Success:
                            if (mod == null) {
                                _modules.TryRemove(name, out _);
                                break;
                            }
                            return mod;
                        case AstPythonInterpreterFactory.TryImportModuleResult.ModuleNotFound:
                            retries = 0;
                            _modules.TryRemove(name, out _);
                            break;
                        case AstPythonInterpreterFactory.TryImportModuleResult.NotSupported:
                            retries = 0;
                            break;
                        case AstPythonInterpreterFactory.TryImportModuleResult.NeedRetry:
                        case AstPythonInterpreterFactory.TryImportModuleResult.Timeout:
                            break;
                    }
                }
            }

            var nameParts = name.Split('.');
            mod = null;
            if (nameParts.Length > 1 && (mod = ImportModule(nameParts[0])) != null) {
                for (int i = 1; i < nameParts.Length && mod != null; ++i) {
                    mod = mod.GetMember(IronPythonModuleContext.ShowClrInstance, nameParts[i]) as IPythonModule;
                }
            }

            return _modules.GetOrAdd(name, mod);
        }

        public IModuleContext CreateModuleContext() {
            return new IronPythonModuleContext();
        }

        public Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken)) {
            switch (reference.Kind) {
                case ProjectReferenceKind.Assembly:
                    var asmRef = (ProjectAssemblyReference)reference;

                    return Task.Factory.StartNew(() => {
                        if (File.Exists(asmRef.Name)) {
                            if (!Remote.LoadAssemblyReference(asmRef.Name)) {
                                throw new Exception("Failed to load assembly: " + asmRef.Name);
                            }
                        } else {
                            if (!Remote.LoadAssemblyReferenceByName(asmRef.AssemblyName.FullName) &&
                                !Remote.LoadAssemblyReferenceByName(asmRef.AssemblyName.Name)) {
                                throw new Exception("Failed to load assembly: " + asmRef.AssemblyName.FullName);
                            }
                        }

                        lock (_projectReferenceSet) {
                            _projectReferenceSet.Add(reference);
                        }

                        // re-analyze clr.AddReference calls w/ new assemblie names
                        ClearAssemblyLoadSet();

                        // the names haven't changed yet, but we want to re-analyze the clr.AddReference calls,
                        // and then the names may change for real..
                        RaiseModuleNamesChanged();
                    });
            }
            return Task.Factory.StartNew(() => { });
        }

        public void RemoveReference(ProjectReference reference) {
            switch (reference.Kind) {
                case ProjectReferenceKind.Assembly:
                    var asmRef = (ProjectAssemblyReference)reference;

                    if (Remote.UnloadAssemblyReference(asmRef.Name)) {
                        ReloadRemoteDomain();

                        lock (_projectReferenceSet) {
                            _projectReferenceSet.Remove(reference);

                            foreach (var prevRef in _projectReferenceSet) {
                                Remote.LoadAssemblyReference(prevRef.Name);
                            }
                        }
                    }
                    break;
            }
        }

        public IEnumerable<ProjectReference> GetReferences() {
            lock (_projectReferenceSet) {
                return _projectReferenceSet.ToArray();
            }
        }

        #endregion

        internal IPythonType GetTypeFromType(ObjectIdentityHandle type) {
            if (type.IsNull) {
                return null;
            }

            lock (this) {
                IMember res;
                if (!_members.TryGetValue(type, out res)) {
                    _members[type] = res = new IronPythonType(this, type);
                }
                return res as IPythonType;
            }
        }

        internal IMember MakeObject(ObjectIdentityHandle obj) {
            if (obj.IsNull) {
                return null;
            }

            lock (this) {
                IMember res;
                if (_members.TryGetValue(obj, out res)) {
                    return res;
                }

                switch (_remote.GetObjectKind(obj)) {
                    case ObjectKind.Module: res = new IronPythonModule(this, obj); break;
                    case ObjectKind.Type: res = new IronPythonType(this, obj); break;
                    case ObjectKind.ConstructorFunction: res = new IronPythonConstructorFunction(this, _remote.GetConstructorFunctionTargets(obj), GetTypeFromType(_remote.GetConstructorFunctionDeclaringType(obj))); break;
                    case ObjectKind.BuiltinFunction: res = new IronPythonBuiltinFunction(this, obj); break;
                    case ObjectKind.BuiltinMethodDesc: res = new IronPythonBuiltinMethodDescriptor(this, obj); break;
                    case ObjectKind.ReflectedEvent: res = new IronPythonEvent(this, obj); break;
                    case ObjectKind.ReflectedExtensionProperty: res = new IronPythonExtensionProperty(this, obj); break;
                    case ObjectKind.ReflectedField: res = new IronPythonField(this, obj); break;
                    case ObjectKind.ReflectedProperty: res = new IronPythonProperty(this, obj); break;
                    case ObjectKind.TypeGroup: res = new IronPythonTypeGroup(this, obj); break;
                    case ObjectKind.NamespaceTracker: res = new IronPythonNamespace(this, obj); break;
                    case ObjectKind.Constant: res = new IronPythonConstant(this, obj); break;
                    case ObjectKind.ClassMethod: res = new IronPythonGenericMember(this, obj, PythonMemberType.Method); break;
                    case ObjectKind.Method: res = new IronPythonGenericMember(this, obj, PythonMemberType.Method); break;
                    case ObjectKind.PythonTypeSlot: res = new IronPythonGenericMember(this, obj, PythonMemberType.Property); break;
                    case ObjectKind.PythonTypeTypeSlot: res = new IronPythonGenericMember(this, obj, PythonMemberType.Property); break;
                    case ObjectKind.Unknown: res = new PythonObject(this, obj); break;
                    default:
                        throw new InvalidOperationException();
                }
                _members[obj] = res;
                return res;
            }
        }

        #region IDotNetPythonInterpreter Members

        public IPythonType GetBuiltinType(Type type) {
            return GetTypeFromType(Remote.GetBuiltinTypeFromType(type));
        }

        #endregion

        class DomainUnloader : IDisposable {
            private readonly AppDomain _domain;
            private bool _isDisposed;

            public DomainUnloader(AppDomain domain) {
                _domain = domain;
            }

            ~DomainUnloader() {
                // The CLR doesn't allow unloading an app domain from the finalizer thread,
                // so instead we unload it from a thread pool thread when we're finalized.  
                ThreadPool.QueueUserWorkItem(Unload);
            }

            private void Unload(object state) {
                try {
                    AppDomain.Unload(_domain);
                } catch (CannotUnloadAppDomainException) {
                    // if we fail to unload, keep trying by creating a new finalizable object...
                    Debug.Fail("should have unloaded");
                    new DomainUnloader(_domain);
                }
            }

            #region IDisposable Members

            public void Dispose() {
                if (!_isDisposed) {
                    _isDisposed = true;
                    AppDomain.Unload(_domain);
                    GC.SuppressFinalize(this);
                }
            }

            #endregion
        }

        #region IDisposable Members

        public void Dispose() {
            var evt = UnloadingDomain;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }

            if (_factory is PythonInterpreterFactoryWithDatabase withDb) {
                withDb.NewDatabaseAvailable -= OnNewDatabaseAvailable;
            }
            _typeDb = null;
            AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolver.Instance.CurrentDomain_AssemblyResolve;
            _unloader.Dispose();
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        #endregion

#if DEBUG
        ~IronPythonInterpreter() {
            Debug.WriteLine(String.Format("IronPythonInterpreter leaked {0}", _id));
        }
#endif
    }
}
