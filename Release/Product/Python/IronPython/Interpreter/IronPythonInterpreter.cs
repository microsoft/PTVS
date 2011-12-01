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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using IronPython.Runtime;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.Win32;
using System.Collections.Concurrent;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreter : IPythonInterpreter, IDotNetPythonInterpreter, IPythonInterpreter2, IDisposable {
        private readonly Dictionary<ObjectIdentityHandle, IMember> _members = new Dictionary<ObjectIdentityHandle, IMember>();
        private readonly ConcurrentDictionary<string, IronPythonModule> _modules = new ConcurrentDictionary<string, IronPythonModule>();
        private readonly ConcurrentBag<string> _assemblyLoadSet = new ConcurrentBag<string>();
        private readonly HashSet<ProjectReference> _projectReferenceSet = new HashSet<ProjectReference>();
        private readonly IronPythonInterpreterFactory _factory;
        private RemoteInterpreter _remote;
        private DomainUnloader _unloader;
        private IInterpreterState _state;
        private PythonTypeDatabase _typeDb;

        public IronPythonInterpreter(IronPythonInterpreterFactory factory) {
            _factory = factory;

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            InitializeRemoteDomain();

            LoadAssemblies();

            LoadModules();
            
            if (factory.ConfigurableDatabaseExists()) {
                LoadNewTypeDb();
            }
        }

        private void InitializeRemoteDomain() {
            var remoteDomain = CreateDomain(out _remote);
            _unloader = new DomainUnloader(remoteDomain);
        }

        private AppDomain CreateDomain(out RemoteInterpreter remoteInterpreter) {
            // We create a sacrificial domain for loading all of our assemblies into.  

            AppDomainSetup setup = new AppDomainSetup();
            setup.ShadowCopyFiles = "true";
            // We are in ...\Extensions\Microsoft\IronPython Interpreter\1.1
            // We need to be able to load assemblies from:
            //      Python Tools for Visual Studio\1.1
            //      IronPython Interpreter\1.1
            //
            // So setup the application base to be Extensions\Microsoft\, and then add the other 2 dirs to the private bin path.
            setup.ApplicationBase = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));
            setup.PrivateBinPath = Path.GetDirectoryName(typeof(IronPythonInterpreter).Assembly.Location) + ";" + 
                                   Path.GetDirectoryName(typeof(IPythonFunction).Assembly.Location);

            setup.PrivateBinPathProbe = "";

            var domain = AppDomain.CreateDomain("IronPythonAnalysisDomain", null, setup);
            
            remoteInterpreter = (RemoteInterpreter)domain.CreateInstanceAndUnwrap(
                typeof(RemoteInterpreter).Assembly.FullName,
                typeof(RemoteInterpreter).FullName);

            return domain;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            if (new AssemblyName(args.Name).FullName == typeof(RemoteInterpreter).Assembly.FullName) {
                return typeof(RemoteInterpreter).Assembly;
            }
            return null;
        }

        public RemoteInterpreter Remote {
            get {
                return _remote;
            }
        }

        private void LoadModules() {
            foreach (string modName in Remote.GetBuiltinModuleNames()) {
                try {
                    var mod = Remote.ImportBuiltinModule(modName);

                    if (modName == "__builtin__") {
                        _modules[modName] = new IronPythonBuiltinModule(this, mod, modName);
                    } else {
                        _modules[modName] = new IronPythonModule(this, mod, modName);
                    }
                } catch {
                    // importing can throw, ignore that module
                    continue;
                }
            }
       }

        public void Initialize(IInterpreterState state) {
            PythonAnalyzer analyzer = _state as PythonAnalyzer;
            if (analyzer != null) {
                analyzer.AnalysisDirectoriesChanged -= AnalysisDirectoryChanged;
            }

            _state = state;
            SpecializeClrFunctions();

            analyzer = _state as PythonAnalyzer;
            if (analyzer != null) {
                analyzer.AnalysisDirectoriesChanged += AnalysisDirectoryChanged;
            }
        }

        private void SpecializeClrFunctions() {
            _state.SpecializeFunction("clr", "AddReference", (n) => AddReference(n, null));
            _state.SpecializeFunction("clr", "AddReferenceByPartialName", (n) => AddReference(n, LoadAssemblyByPartialName));
            _state.SpecializeFunction("clr", "AddReferenceByName", (n) => AddReference(n, null));
            _state.SpecializeFunction("clr", "AddReferenceToFile", (n) => AddReference(n, LoadAssemblyFromFile));
            _state.SpecializeFunction("clr", "AddReferenceToFileAndPath", (n) => AddReference(n, LoadAssemblyFromFileWithPath));
        }

        private void AnalysisDirectoryChanged(object sender, EventArgs e) {
            switch(_remote.SetAnalysisDirectories(_state.AnalysisDirectories.ToArray())) {
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

        internal static string GetPythonInstallDir() {
            using (var ipy = Registry.LocalMachine.OpenSubKey("SOFTWARE\\IronPython")) {
                if (ipy != null) {
                    using (var twoSeven = ipy.OpenSubKey("2.7")) {
                        if (twoSeven != null) {
                            var installPath = twoSeven.OpenSubKey("InstallPath");
                            if (installPath != null) {
                                var res = installPath.GetValue("") as string;
                                if (res != null) {
                                    return res;
                                }
                            }
                        }
                    }
                }
            }

            var paths = Environment.GetEnvironmentVariable("PATH");
            if (paths != null) {
                foreach (string dir in paths.Split(Path.PathSeparator)) {
                    try {
                        if (IronPythonExistsIn(dir)) {
                            return dir;
                        }
                    } catch {
                        // ignore
                    }
                }
            }

            return null;
        }

        private static bool IronPythonExistsIn(string/*!*/ dir) {
            return File.Exists(Path.Combine(dir, "ipy.exe"));
        }

        private void AddReference(Microsoft.PythonTools.Parsing.Ast.CallExpression node, Func<string, ObjectHandle> partialLoader) {
            // processes a call to clr.AddReference updating project state
            // so that it contains the newly loaded assembly.
            foreach (var arg in node.Args) {
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
                                asm = LoadAssemblyByPartialName(asmName);
                            }
                        }

                        if (asm == null && _state != null) {
                            var invalidPathChars = Path.GetInvalidPathChars();
                            foreach (var dir in _state.AnalysisDirectories) {
                                if (dir.IndexOfAny(invalidPathChars) == -1 && asmName.IndexOfAny(invalidPathChars) == -1) {

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
        }

        internal void RaiseModuleNamesChanged() {
            var modNamesChanged = ModuleNamesChanged;
            if (modNamesChanged != null) {
                modNamesChanged(this, EventArgs.Empty);
            }
        }

        #region IPythonInterpreter Members

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            return GetTypeFromType(Remote.GetBuiltinType(id));
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

        public IronPythonModule GetModule(string name) {
            return _modules[name];
        }

        public IPythonModule ImportModule(string name) {
            if (!String.IsNullOrWhiteSpace(name)) {
                if (_typeDb != null) {
                    var res = _typeDb.GetModule(name);
                    if (res != null) {
                        return res;
                    }
                }

                IronPythonModule mod;
                if (_modules.TryGetValue(name, out mod)) {
                    return mod;
                }
                
                var handle = Remote.LookupNamespace(name);
                if (!handle.IsNull) {
                    return MakeObject(handle) as IPythonModule;
                }
            }

            return null;
        }

        public IModuleContext CreateModuleContext() {
            return new IronPythonModuleContext();
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
                    case ObjectKind.Method: res = res = new IronPythonGenericMember(this, obj, PythonMemberType.Method); break;
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

        internal void LoadNewTypeDb() {
            IronPythonBuiltinModule builtin;
            lock (this) {
                builtin = (IronPythonBuiltinModule)_modules["__builtin__"];
            }

            _typeDb = new PythonTypeDatabase(_factory.GetConfiguredDatabasePath(), false, builtin);
        }

        class DomainUnloader : IDisposable {
            private readonly AppDomain _domain;

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
                AppDomain.Unload(_domain);
                GC.SuppressFinalize(this);
            }

            #endregion
        }

        #region IDisposable Members

        public void Dispose() {
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            _unloader.Dispose();
        }

        #endregion

        #region IPythonInterpreter2 Members

        public Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken)) {
            switch (reference.Kind) {
                case ProjectReferenceKind.Assembly:
                    var asmRef = (ProjectAssemblyReference)reference;

                    return Task.Factory.StartNew(() => {
                        if (!Remote.LoadAssemblyReference(asmRef.Name)) {
                            throw new Exception("Failed to load assembly: "+ asmRef.Name);
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
            return Task.Factory.StartNew(() => {});
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

        #endregion
    }
}
