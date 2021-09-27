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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.IronPythonTools.Interpreter
{
    /// <summary>
    /// Wraps a Python interpreter which is loaded into a remote app domain.  Provides lots of helper
    /// methods for inspecting various IronPython objects in the domain and returning results w/o causing
    /// type loads in the local domain.  We use ObjectIdentityHandle's to pass the objects back and forth
    /// which allows the local domain to cache based upon object identity w/o transitioning to this domain
    /// to do comparisons.
    /// </summary>
    class RemoteInterpreter
    {
        private readonly ScriptEngine _engine;
        private readonly CodeContext _codeContext;
        private readonly CodeContext _codeContextCls;

        private readonly TopNamespaceTracker _namespaceTracker;
        private readonly Dictionary<object, ObjectIdentityHandle> _members = new Dictionary<object, ObjectIdentityHandle>();
        private readonly List<object> _reverseMembers = new List<object>();
        private readonly HashSet<string> _assembliesLoadedFromDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Assembly> _referencedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private string[] _analysisDirs;

        private const string _codeCtxType = "IronPython.Runtime.CodeContext";

        public RemoteInterpreter()
            : this(Python.CreateEngine(new Dictionary<string, object> { { "NoAssemblyResolveHook", true } }))
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFile")]
        public RemoteInterpreter(ScriptEngine engine)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            _engine = engine;

            var pythonContext = HostingHelpers.GetLanguageContext(_engine) as PythonContext;
            _codeContextCls = new ModuleContext(new PythonDictionary(), pythonContext).GlobalContext;
            _codeContextCls.ModuleContext.ShowCls = true;

            _codeContext = new ModuleContext(
                new PythonDictionary(),
                HostingHelpers.GetLanguageContext(_engine) as PythonContext
                ).GlobalContext;

            _namespaceTracker = new TopNamespaceTracker(_codeContext.LanguageContext.DomainManager);

            AddAssembly(LoadAssemblyInfo(typeof(string).Assembly));
            AddAssembly(LoadAssemblyInfo(typeof(Debug).Assembly));

            LoadAssemblies();
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (new AssemblyName(args.Name).FullName == typeof(RemoteInterpreterProxy).Assembly.FullName)
            {
                return typeof(RemoteInterpreterProxy).Assembly;
            }

            if (_analysisDirs != null)
            {
                foreach (var dir in _analysisDirs)
                {
                    var name = new AssemblyName(args.Name).Name;
                    var asm = TryLoad(dir, name, "");
                    if (asm != null)
                    {
                        AddLoadedAssembly(dir);
                        return asm;
                    }
                    asm = TryLoad(dir, name, ".dll");
                    if (asm != null)
                    {
                        AddLoadedAssembly(dir);
                        return asm;
                    }
                    asm = TryLoad(dir, name, ".exe");
                    if (asm != null)
                    {
                        AddLoadedAssembly(dir);
                        return asm;
                    }
                }
            }

            return null;
        }

        private void AddLoadedAssembly(string dir)
        {
            lock (_assembliesLoadedFromDirectories)
            {
                _assembliesLoadedFromDirectories.Add(dir);
            }
        }

        private Assembly TryLoad(string dir, string name, string ext)
        {
            string path = Path.Combine(dir, name + ext);
            if (File.Exists(path))
            {
                try
                {
                    return Assembly.Load(File.ReadAllBytes(path));
                }
                catch
                {
                }
            }
            return null;
        }

        internal string[] GetBuiltinModuleNames()
        {
            var names = _engine.Operations.GetMember<PythonTuple>(_engine.GetSysModule(), "builtin_module_names");
            string[] res = new string[names.Count];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = (string)names[i];
            }
            return res;
        }

        internal ObjectIdentityHandle ImportBuiltinModule(string modName)
        {
            PythonModule mod = Importer.Import(_codeContextCls, modName, PythonOps.EmptyTuple, 0) as PythonModule;
            Debug.Assert(mod != null);
            return MakeHandle(mod);
        }

        /// <summary>
        /// VS seems to load extensions via Assembly.LoadFrom. When an assembly is being loaded via Assembly.Load the CLR fusion probes privatePath 
        /// set in App.config (devenv.exe.config) first and then tries the code base of the assembly that called Assembly.Load if it was itself loaded via LoadFrom. 
        /// In order to locate IronPython.Modules correctly, the call to Assembly.Load must originate from an assembly in IronPythonTools installation folder. 
        /// Although Microsoft.Scripting is also in that folder it can be loaded first by IronRuby and that causes the Assembly.Load to search in IronRuby's 
        /// installation folder. Adding a reference to IronPython.Modules also makes sure that the assembly is loaded from the same location as IronPythonToolsCore.
        /// </summary>
        private static void LoadAssemblies()
        {
            GC.KeepAlive(typeof(IronPython.Modules.ArrayModule)); // IronPython.Modules
        }

        public ScriptEngine Engine
        {
            get
            {
                return _engine;
            }
        }

        private KeyValuePair<Assembly, TopNamespaceTracker> LoadAssemblyInfo(Assembly assm)
        {
            var nsTracker = new TopNamespaceTracker(_codeContext.LanguageContext.DomainManager);
            nsTracker.LoadAssembly(assm);
            return new KeyValuePair<Assembly, TopNamespaceTracker>(assm, nsTracker);
        }

        #region CallAndHandle helpers

        private static ObjectHandle CallAndHandle(Func<ObjectHandle> action)
        {
            try
            {
                return action();
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (CannotUnwrapHandleException)
            {
            }
            return null;
        }

        private static ObjectIdentityHandle CallAndHandle(Func<ObjectIdentityHandle> action)
        {
            try
            {
                return action();
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (CannotUnwrapHandleException)
            {
            }
            return new ObjectIdentityHandle();
        }

        private static string CallAndHandle(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (CannotUnwrapHandleException)
            {
            }
            return string.Empty;
        }

        private static string[] CallAndHandle(Func<string[]> action)
        {
            try
            {
                return action();
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (CannotUnwrapHandleException)
            {
            }
            return new string[0];
        }

        private static T CallAndHandle<T>(Func<T> action, T defaultValue)
        {
            try
            {
                return action();
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (CannotUnwrapHandleException)
            {
            }
            return defaultValue;
        }

        private static T CallAndHandle<T>(Func<T> action, Func<T> defaultValueCreator)
        {
            try
            {
                return action();
            }
            catch (FileLoadException)
            {
            }
            catch (IOException)
            {
            }
            catch (CannotUnwrapHandleException)
            {
            }
            return defaultValueCreator();
        }

        #endregion

        internal ObjectHandle LoadAssemblyByName(string name)
        {
            return CallAndHandle(() =>
            {
                Assembly res;
                if (_referencedAssemblies.TryGetValue(name, out res) ||
                    (res = ClrModule.LoadAssemblyByName(CodeContext, name)) != null)
                {
                    return new ObjectHandle(res);
                }
                return null;
            });
        }

        internal ObjectHandle LoadAssemblyByPartialName(string name)
        {
            return CallAndHandle(() =>
            {
                var res = ClrModule.LoadAssemblyByPartialName(name);
                if (res != null)
                {
                    return new ObjectHandle(res);
                }
                return null;
            });
        }

        internal ObjectHandle LoadAssemblyFromFile(string name)
        {
            return CallAndHandle(() =>
            {
                var res = ClrModule.LoadAssemblyFromFile(CodeContext, name);
                if (res != null)
                {
                    return new ObjectHandle(res);
                }
                return null;
            });
        }

        internal ObjectHandle LoadAssemblyFromFileWithPath(string name)
        {
            return CallAndHandle(() =>
            {
                var res = ClrModule.LoadAssemblyFromFileWithPath(CodeContext, name);
                if (res != null)
                {
                    return new ObjectHandle(res);
                }
                return null;
            });
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFrom")]
        internal ObjectHandle LoadAssemblyFrom(string path)
        {
            return CallAndHandle(() =>
            {
                var res = Assembly.LoadFrom(path);
                if (res != null)
                {
                    return new ObjectHandle(res);
                }
                return null;
            });
        }

        private void AddAssembly(KeyValuePair<Assembly, TopNamespaceTracker> assembly)
        {
            _engine.Runtime.LoadAssembly(assembly.Key);
            _namespaceTracker.LoadAssembly(assembly.Key);
        }

        public bool AddAssembly(ObjectHandle assembly)
        {
            return CallAndHandle(() =>
            {
                var asm = (Assembly)assembly.Unwrap();
                return AddAssembly(asm);
            }, false);
        }

        private bool AddAssembly(Assembly asm)
        {
            if (asm != null && !_namespaceTracker.PackageAssemblies.Contains(asm))
            {
                _engine.Runtime.LoadAssembly(asm);
                return _namespaceTracker.LoadAssembly(asm);
            }
            return false;
        }

        public bool LoadWpf()
        {
            return CallAndHandle(() =>
            {
                var res = AddAssembly(typeof(System.Windows.Markup.XamlReader).Assembly);  // PresentationFramework
                res |= AddAssembly(typeof(System.Windows.Clipboard).Assembly);             // PresentationCore
                res |= AddAssembly(typeof(System.Windows.DependencyProperty).Assembly);    // WindowsBase
                res |= AddAssembly(typeof(System.Xaml.XamlReader).Assembly);               // System.Xaml
                return res;
            }, false);
        }

        public IList<string> GetModuleNames()
        {
            return CallAndHandle(() =>
            {
                return _namespaceTracker.Keys.ToArray();
            });
        }

        public ObjectIdentityHandle LookupNamespace(string name)
        {
            if (!String.IsNullOrWhiteSpace(name))
            {
                var ns = _namespaceTracker.TryGetPackage(name);
                if (ns != null)
                {
                    return MakeHandle(ns);
                }
            }

            return new ObjectIdentityHandle();
        }

        internal bool TryGetMember(object obj, string name, out object value)
        {
            return TryGetMember(_codeContext, obj, name, out value);
        }

        internal bool TryGetMember(object obj, string name, bool showClr, out object value)
        {
            var cctx = showClr ? _codeContextCls : _codeContext;
            return TryGetMember(cctx, obj, name, out value);
        }

        private bool TryGetMember(CodeContext codeContext, object obj, string name, out object value)
        {
            NamespaceTracker nt = obj as NamespaceTracker;
            if (nt != null)
            {
                value = NamespaceTrackerOps.GetCustomMember(codeContext, nt, name);
                return value != OperationFailed.Value;
            }

            object result = Builtin.getattr(codeContext, obj, name, this);
            if (result == this)
            {
                value = null;
                return false;
            }
            else
            {
                value = result;
                return true;
            }
        }

        public CodeContext CodeContext
        {
            get
            {
                return _codeContext;
            }
        }

        internal string[] DirHelper(ObjectIdentityHandle handle, bool showClr)
        {
            return CallAndHandle(() =>
            {
                var obj = Unwrap(handle);

                NamespaceTracker nt = obj as NamespaceTracker;
                if (nt != null)
                {
                    return nt.GetMemberNames().ToArray();
                }

                var dir = TryDir(obj, showClr);
                int len = dir.__len__();
                string[] result = new string[len];
                for (int i = 0; i < len; i++)
                {
                    // TODO: validate
                    result[i] = dir[i] as string;
                }
                return result;
            });
        }

        private static List TryDir(object obj, bool showClr)
        {
            try
            {
                return showClr ? ClrModule.DirClr(obj) : ClrModule.Dir(obj);
            }
            catch
            {
                // http://pytools.codeplex.com/discussions/279363#post697979
                // Work around exceptions coming out of IronPython and the CLR,
                // one wouldn't normally expect dir() to throw but it can...
                return new List();
            }
        }

        private ObjectIdentityHandle MakeHandle(object member)
        {
            if (member == null)
            {
                return new ObjectIdentityHandle();
            }
            lock (_members)
            {
                ObjectIdentityHandle handle;

                if (!_members.TryGetValue(member, out handle))
                {
                    _members[member] = handle = new ObjectIdentityHandle(_members.Count + 1);
                    _reverseMembers.Add(member);
                }

                return handle;
            }
        }

        [Serializable]
        private class CannotUnwrapHandleException : Exception
        {
            public CannotUnwrapHandleException() { }
            public CannotUnwrapHandleException(string message) : base(message) { }
            public CannotUnwrapHandleException(string message, Exception inner) : base(message, inner) { }
            protected CannotUnwrapHandleException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context)
                : base(info, context) { }
        }

        private object Unwrap(ObjectIdentityHandle handle)
        {
            if (handle.IsNull)
            {
                throw new CannotUnwrapHandleException();
            }

            lock (_members)
            {
                if (handle.Id > _reverseMembers.Count)
                {
                    Debug.Fail("Invalid object identity handle");
                    throw new CannotUnwrapHandleException();
                }
                var result = _reverseMembers[handle.Id - 1];
                if (result == null)
                {
                    throw new CannotUnwrapHandleException();
                }
                return result;
            }
        }

        internal ObjectKind GetObjectKind(ObjectIdentityHandle value)
        {
            if (value.IsNull)
            {
                return ObjectKind.Constant;
            }

            var obj = Unwrap(value);

            if (obj is PythonModule)
            {
                return ObjectKind.Module;
            }
            else if (obj is PythonType)
            {
                return ObjectKind.Type;
            }
            else if (obj is ConstructorFunction)
            {
                return ObjectKind.ConstructorFunction;
            }
            else if (obj is BuiltinFunction)
            {
                return ObjectKind.BuiltinFunction;
            }
            else if (obj is BuiltinMethodDescriptor)
            {
                return ObjectKind.BuiltinMethodDesc;
            }
            else if (obj is ReflectedField)
            {
                return ObjectKind.ReflectedField;
            }
            else if (obj is ReflectedProperty)
            {
                return ObjectKind.ReflectedProperty;
            }
            else if (obj is ReflectedExtensionProperty)
            {
                return ObjectKind.ReflectedExtensionProperty;
            }
            else if (obj is NamespaceTracker)
            {
                return ObjectKind.NamespaceTracker;
            }
            else if (obj is Method)
            {
                return ObjectKind.Method;
            }
            else if (obj is ClassMethodDescriptor)
            {
                return ObjectKind.ClassMethod;
            }
            else if (obj is PythonTypeTypeSlot)
            {
                return ObjectKind.PythonTypeTypeSlot;
            }
            else if (obj is ReflectedEvent)
            {
                return ObjectKind.ReflectedEvent;
            }
            else if (obj is PythonTypeSlot)
            {
                return ObjectKind.PythonTypeSlot;
            }
            else if (obj is TypeGroup)
            {
                return ObjectKind.TypeGroup;
            }
            else if (obj is bool || obj is int || obj is Complex || obj is string || obj is long || obj is double ||
                     obj.GetType().IsEnum)
            {
                return ObjectKind.Constant;
            }

            return ObjectKind.Unknown;
        }

        #region BuiltinMethodDescriptor Helpers

        internal ObjectIdentityHandle GetBuiltinMethodDescriptorTemplate(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var func = PythonOps.GetBuiltinMethodDescriptorTemplate((BuiltinMethodDescriptor)Unwrap(handle));

                return MakeHandle(func);
            });
        }

        #endregion

        #region PythonModule Helpers

        internal string GetModuleDocumentation(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                PythonModule module = (PythonModule)Unwrap(handle);

                object docValue;
                if (!module.Get__dict__().TryGetValue("__doc__", out docValue) || !(docValue is string))
                {
                    return String.Empty;
                }
                return (string)docValue;
            });
        }

        internal string GetModuleName(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                PythonModule module = (PythonModule)Unwrap(handle);

                object name;
                if (!module.Get__dict__().TryGetValue("__name__", out name) || !(name is string))
                {
                    name = "";
                }
                return (string)name;
            });
        }

        #endregion

        #region PythonType Helpers

        internal string GetPythonTypeName(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                return PythonType.Get__name__((PythonType)Unwrap(handle));
            });
        }

        internal string GetPythonTypeDocumentation(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                try
                {
                    return PythonType.Get__doc__(CodeContext, (PythonType)Unwrap(handle)) as string;
                }
                catch (ArgumentException)
                {
                    // IronPython can throw here if it can't figure out the 
                    // path of the assembly this type is defined in.
                    return null;
                }
            });
        }

        internal ObjectIdentityHandle[] GetPythonTypeMro(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var mro = PythonType.Get__mro__((PythonType)Unwrap(handle));
                var mroHandles = new List<ObjectIdentityHandle>();
                foreach (var item in mro.OfType<PythonType>())
                {
                    mroHandles.Add(MakeHandle(item));
                }
                return mroHandles.ToArray();
            }, () => new ObjectIdentityHandle[0]);
        }

        internal BuiltinTypeId PythonTypeGetBuiltinTypeId(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(handle);

                var clrType = value.__clrtype__();

                switch (Type.GetTypeCode(clrType))
                {
                    case TypeCode.Boolean: return BuiltinTypeId.Bool;
                    case TypeCode.Int32: return BuiltinTypeId.Int;
                    case TypeCode.String: return BuiltinTypeId.Unicode;
                    case TypeCode.Double: return BuiltinTypeId.Float;
                    case TypeCode.Object:
                        if (clrType == typeof(object))
                        {
                            return BuiltinTypeId.Object;
                        }
                        else if (clrType == typeof(PythonFunction))
                        {
                            return BuiltinTypeId.Function;
                        }
                        else if (clrType == typeof(BuiltinFunction))
                        {
                            return BuiltinTypeId.BuiltinFunction;
                        }
                        else if (clrType == typeof(BuiltinMethodDescriptor))
                        {
                            return BuiltinTypeId.BuiltinMethodDescriptor;
                        }
                        else if (clrType == typeof(Complex))
                        {
                            return BuiltinTypeId.Complex;
                        }
                        else if (clrType == typeof(PythonDictionary))
                        {
                            return BuiltinTypeId.Dict;
                        }
                        else if (clrType == typeof(BigInteger))
                        {
                            return BuiltinTypeId.Long;
                        }
                        else if (clrType == typeof(List))
                        {
                            return BuiltinTypeId.List;
                        }
                        else if (clrType == typeof(PythonGenerator))
                        {
                            return BuiltinTypeId.Generator;
                        }
                        else if (clrType == typeof(SetCollection))
                        {
                            return BuiltinTypeId.Set;
                        }
                        else if (clrType == typeof(PythonType))
                        {
                            return BuiltinTypeId.Type;
                        }
                        else if (clrType == typeof(PythonTuple))
                        {
                            return BuiltinTypeId.Tuple;
                        }
                        else if (clrType == typeof(Bytes))
                        {
                            return BuiltinTypeId.Bytes;
                        }
                        return BuiltinTypeId.Type;
                }
                return BuiltinTypeId.Unknown;
            }, BuiltinTypeId.Unknown);
        }

        internal string GetTypeDeclaringModule(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);

                return (string)PythonType.Get__module__(CodeContext, value);
            });
        }

        internal bool IsPythonTypeArray(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);

                return value.__clrtype__().IsArray;
            }, false);
        }

        internal ObjectIdentityHandle GetPythonTypeElementType(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);
                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(value.__clrtype__().GetElementType()));
            });
        }

        internal bool IsDelegateType(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);

                return typeof(Delegate).IsAssignableFrom(value.__clrtype__());
            }, false);
        }

        internal ObjectIdentityHandle[] GetEventInvokeArgs(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);
                var type = value.__clrtype__();

                return GetEventInvokeArgs(type);
            }, () => new ObjectIdentityHandle[0]);
        }

        private ObjectIdentityHandle[] GetEventInvokeArgs(Type type)
        {
            return CallAndHandle(() =>
            {
                var p = type.GetMethod("Invoke").GetParameters();

                var args = new ObjectIdentityHandle[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    args[i] = MakeHandle(DynamicHelpers.GetPythonTypeFromType(p[i].ParameterType));
                }
                return args;
            }, () => new ObjectIdentityHandle[0]);
        }

        internal PythonMemberType GetPythonTypeMemberType(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);
                var type = value.__clrtype__();
                if (type.IsEnum)
                {
                    return PythonMemberType.Enum;
                }
                else if (typeof(Delegate).IsAssignableFrom(type))
                {
                    return PythonMemberType.Delegate;
                }
                else
                {
                    return PythonMemberType.Class;
                }
            }, PythonMemberType.Class);
        }

        internal bool PythonTypeHasNewOrInitMethods(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);
                return PythonTypeHasNewOrInitMethods(value);
            }, false);
        }

        private static bool PythonTypeHasNewOrInitMethods(PythonType value)
        {
            return CallAndHandle(() =>
            {
                var clrType = ClrModule.GetClrType(value);

                if (!(value == TypeCache.String ||
                        value == TypeCache.Object ||
                        value == TypeCache.Double ||
                        value == TypeCache.Complex ||
                        value == TypeCache.Boolean))
                {
                    var newMethods = clrType.GetMember("__new__", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static);

                    if (newMethods.Length == 0)
                    {
                        var initMethods = clrType.GetMember("__init__", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance);

                        return initMethods.Length != 0;
                    }
                    return true;
                }
                else if (clrType == typeof(object))
                {
                    return false;
                }
                return true;
            }, false);
        }

        internal ObjectIdentityHandle PythonTypeMakeGenericType(ObjectIdentityHandle pythonType, ObjectIdentityHandle[] types)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);
                Type[] realTypes = new Type[types.Length];
                for (int i = 0; i < types.Length; i++)
                {
                    realTypes[i] = ((PythonType)Unwrap(types[i])).__clrtype__();
                }

                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(value.__clrtype__().MakeGenericType(realTypes)));
            });
        }

        internal ObjectIdentityHandle[] GetPythonTypeConstructors(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);
                Type clrType = ClrModule.GetClrType(value);

                var ctors = clrType.GetConstructors(BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance);
                if (ctors.Length > 0)
                {
                    ObjectIdentityHandle[] res = new ObjectIdentityHandle[ctors.Length];
                    for (int i = 0; i < res.Length; i++)
                    {
                        res[i] = MakeHandle(ctors[i]);
                    }
                    return res;
                }
                return null;
            }, () => new ObjectIdentityHandle[0]);
        }

        internal bool IsPythonTypeGenericTypeDefinition(ObjectIdentityHandle pythonType)
        {
            return CallAndHandle(() =>
            {
                var value = (PythonType)Unwrap(pythonType);

                return value.__clrtype__().IsGenericTypeDefinition;
            }, false);
        }

        #endregion

        #region ReflectedField Helpers

        internal string GetFieldDocumentation(ObjectIdentityHandle field)
        {
            return CallAndHandle(() =>
            {
                var value = (ReflectedField)Unwrap(field);
                try
                {
                    return value.__doc__;
                }
                catch (ArgumentException)
                {
                    // IronPython can throw here if it can't figure out the 
                    // path of the assembly this type is defined in.
                    return "";
                }
            });
        }

        /// <summary>
        /// Returns an ObjectIdentityHandle which contains a PythonType
        /// </summary>
        internal ObjectIdentityHandle GetFieldType(ObjectIdentityHandle field)
        {
            return CallAndHandle(() =>
            {
                var value = (ReflectedField)Unwrap(field);

                return MakeHandle(value.FieldType);
            });
        }

        internal bool IsFieldStatic(ObjectIdentityHandle field)
        {
            return CallAndHandle(() =>
            {
                var value = (ReflectedField)Unwrap(field);

                return value.Info.IsStatic;
            }, false);
        }

        #endregion

        #region BuiltinFunction Helpers

        internal string GetBuiltinFunctionName(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
            {
                BuiltinFunction func = (BuiltinFunction)Unwrap(function);
                return func.__name__;
            });
        }

        internal string GetBuiltinFunctionDocumentation(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
            {
                BuiltinFunction func = (BuiltinFunction)Unwrap(function);
                try
                {
                    return func.__doc__;
                }
                catch (ArgumentException)
                {
                    // IronPython can throw here if it can't figure out the 
                    // path of the assembly this type is defined in.
                    return "";
                }
            });
        }

        internal string GetBuiltinFunctionModule(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
            {
                BuiltinFunction func = (BuiltinFunction)Unwrap(function);
                return func.Get__module__(CodeContext);
            });
        }

        internal ObjectIdentityHandle GetBuiltinFunctionDeclaringPythonType(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
            {
                BuiltinFunction func = (BuiltinFunction)Unwrap(function);
                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(func.DeclaringType));
            });
        }

        internal ObjectIdentityHandle[] GetBuiltinFunctionOverloads(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
            {
                BuiltinFunction func = (BuiltinFunction)Unwrap(function);
                var result = new List<ObjectIdentityHandle>();
                foreach (var ov in func.Overloads.Functions)
                {
                    BuiltinFunction overload = (ov as BuiltinFunction);
                    if (overload.Overloads.Targets[0].DeclaringType.IsAssignableFrom(func.DeclaringType) ||
                        (overload.Overloads.Targets[0].DeclaringType.FullName != null &&
                        overload.Overloads.Targets[0].DeclaringType.FullName.StartsWithOrdinal("IronPython.Runtime.Operations.")))
                    {
                        result.Add(MakeHandle(overload.Targets[0]));
                    }
                }

                return result.ToArray();
            }, () => new ObjectIdentityHandle[0]);
        }

        internal ObjectIdentityHandle[] GetConstructorFunctionTargets(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
            {
                return ((ConstructorFunction)Unwrap(function)).Overloads.Targets
                .Select(x => MakeHandle(x))
                .ToArray();
            }, () => Array.Empty<ObjectIdentityHandle>());
        }

        internal ObjectIdentityHandle GetConstructorFunctionDeclaringType(ObjectIdentityHandle function)
        {
            return CallAndHandle(() =>
                MakeHandle(
                    GetTypeFromType(
                        ((ConstructorFunction)Unwrap(function)).Overloads.Targets
                        .First()
                        .DeclaringType
                    )
                )
            );
        }

        #endregion

        #region ReflectedProperty Helpers

        internal ObjectIdentityHandle GetPropertyType(ObjectIdentityHandle property)
        {
            return CallAndHandle(() =>
            {
                ReflectedProperty prop = (ReflectedProperty)Unwrap(property);

                return MakeHandle(prop.PropertyType);
            });
        }

        internal bool IsPropertyStatic(ObjectIdentityHandle property)
        {
            return CallAndHandle(() =>
            {
                ReflectedProperty prop = (ReflectedProperty)Unwrap(property);

                var method = prop.Info.GetGetMethod() ?? prop.Info.GetSetMethod();
                if (method != null)
                {
                    return method.IsStatic;
                }
                return false;
            }, false);
        }

        internal string GetPropertyDocumentation(ObjectIdentityHandle property)
        {
            return CallAndHandle(() =>
            {
                ReflectedProperty prop = (ReflectedProperty)Unwrap(property);

                try
                {
                    return prop.__doc__;
                }
                catch (ArgumentException)
                {
                    // IronPython can throw here if it can't figure out the 
                    // path of the assembly this type is defined in.
                    return "";
                }
            });
        }

        #endregion

        #region Object Helpers

        internal ObjectIdentityHandle GetObjectPythonType(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                return MakeHandle(DynamicHelpers.GetPythonType(Unwrap(value)));
            });
        }

        internal bool IsEnumValue(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var obj = Unwrap(value).GetType();
                return obj.IsEnum;
            }, false);
        }

        internal ObjectIdentityHandle GetMember(ObjectIdentityHandle from, string name)
        {
            return CallAndHandle(() =>
            {
                var obj = Unwrap(from);

                PythonType pyType = (obj as PythonType);
                if (pyType != null)
                {
                    foreach (var baseType in pyType.mro())
                    {
                        PythonType curType = (baseType as PythonType);
                        if (curType != null)
                        {
                            IDictionary<object, object> dict = new DictProxy(curType);
                            object bresult;
                            // reflection can throw while resolving references, ignore the member...
                            // http://pytools.codeplex.com/workitem/612
                            try
                            {
                                if (dict.TryGetValue(name, out bresult))
                                {
                                    return MakeHandle(bresult);
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                var tracker = obj as NamespaceTracker;
                if (tracker != null)
                {
                    object value = NamespaceTrackerOps.GetCustomMember(CodeContext, tracker, name);
                    if (value != OperationFailed.Value)
                    {
                        return MakeHandle(value);
                    }
                    else
                    {
                        return MakeHandle(obj);
                    }
                }

                object result;
                if (TryGetMember(obj, name, true, out result))
                {
                    return MakeHandle(result);
                }

                return MakeHandle(obj);
            });
        }

        #endregion

        #region ReflectedEvent Helpers

        internal string GetEventDocumentation(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var eventObj = (ReflectedEvent)Unwrap(value);
                try
                {
                    return eventObj.__doc__;
                }
                catch (ArgumentException)
                {
                    // IronPython can throw here if it can't figure out the 
                    // path of the assembly this type is defined in.
                    return "";
                }
            });
        }

        internal ObjectIdentityHandle GetEventPythonType(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var eventObj = (ReflectedEvent)Unwrap(value);
                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(eventObj.Info.EventHandlerType));
            });
        }

        internal ObjectIdentityHandle[] GetEventParameterPythonTypes(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var eventObj = (ReflectedEvent)Unwrap(value);

                var parameters = eventObj.Info.EventHandlerType.GetMethod("Invoke").GetParameters();
                var res = new ObjectIdentityHandle[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    res[i] = MakeHandle(DynamicHelpers.GetPythonTypeFromType(parameters[i].ParameterType));
                }

                return res;
            }, () => new ObjectIdentityHandle[0]);
        }

        #endregion

        #region NamespaceTracker Helpers

        internal string GetNamespaceName(ObjectIdentityHandle ns)
        {
            return CallAndHandle(() =>
            {
                var reflNs = (NamespaceTracker)Unwrap(ns);
                return reflNs.Name;
            });
        }

        internal string[] GetNamespaceChildren(ObjectIdentityHandle ns)
        {
            return CallAndHandle(() =>
            {
                List<string> names = new List<string>();
                var reflNs = (NamespaceTracker)Unwrap(ns);
                foreach (var name in reflNs.GetMemberNames() ?? Enumerable.Empty<string>())
                {
                    if (reflNs[name] is NamespaceTracker)
                    {
                        names.Add(name);
                    }
                }
                return names.ToArray();
            });
        }

        #endregion

        #region TypeGroup Helpers

        internal bool TypeGroupHasNewOrInitMethods(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);
                foreach (var type in value.Types)
                {
                    if (PythonTypeHasNewOrInitMethods(DynamicHelpers.GetPythonTypeFromType(type)))
                    {
                        return true;
                    }
                }
                return false;
            }, false);
        }

        internal ObjectIdentityHandle TypeGroupMakeGenericType(ObjectIdentityHandle typeGroup, ObjectIdentityHandle[] types)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);
                var genType = value.GetTypeForArity(types.Length);
                if (genType != null)
                {
                    Type[] genTypes = new Type[types.Length];
                    for (int i = 0; i < types.Length; i++)
                    {
                        var o = Unwrap(types[i]);
                        genTypes[i] = o as Type ?? (PythonType)o;
                    }
                    return MakeHandle(DynamicHelpers.GetPythonTypeFromType(genType.Type.MakeGenericType(genTypes)));
                }
                return new ObjectIdentityHandle();
            });
        }

        internal bool TypeGroupIsGenericTypeDefinition(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);
                foreach (var type in value.Types)
                {
                    if (type.IsGenericTypeDefinition)
                    {
                        return true;
                    }
                }
                return false;
            }, false);
        }

        internal string GetTypeGroupDocumentation(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);
                StringBuilder res = new StringBuilder();
                foreach (var type in value.Types)
                {
                    try
                    {
                        res.Append(PythonType.Get__doc__(CodeContext, DynamicHelpers.GetPythonTypeFromType(type)) as string);
                    }
                    catch (ArgumentException)
                    {
                        // IronPython can throw here if it can't figure out the 
                        // path of the assembly this type is defined in.
                    }
                }
                return res.ToString();
            });
        }

        internal string GetTypeGroupName(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);
                return value.Name;
            });
        }

        internal string GetTypeGroupDeclaringModule(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);
                return (string)PythonType.Get__module__(
                    CodeContext,
                    DynamicHelpers.GetPythonTypeFromType(value.Types.First())
                );
            });
        }

        internal PythonMemberType GetTypeGroupMemberType(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var value = (TypeGroup)Unwrap(typeGroup);

                foreach (var type in value.Types)
                {
                    if (type.IsEnum)
                    {
                        return PythonMemberType.Enum;
                    }
                    else if (typeof(Delegate).IsAssignableFrom(type))
                    {
                        return PythonMemberType.Delegate;
                    }
                }
                return PythonMemberType.Class;
            }, PythonMemberType.Class);
        }

        internal ObjectIdentityHandle[] GetTypeGroupConstructors(ObjectIdentityHandle typeGroup, out ObjectIdentityHandle declaringType)
        {
            var innerDeclaringType = new ObjectIdentityHandle();
            declaringType = new ObjectIdentityHandle();
            var result = CallAndHandle(() =>
            {
                var self = (TypeGroup)Unwrap(typeGroup);

                foreach (var clrType in self.Types)
                {
                    // just a normal .NET type...
                    var ctors = clrType.GetConstructors(BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.Instance);
                    if (ctors.Length > 0)
                    {
                        var res = new ObjectIdentityHandle[ctors.Length];
                        for (int i = 0; i < res.Length; i++)
                        {
                            res[i] = MakeHandle(ctors[i]);
                        }
                        innerDeclaringType = MakeHandle(clrType);
                        return res;
                    }
                }
                innerDeclaringType = default(ObjectIdentityHandle);
                return null;
            }, (ObjectIdentityHandle[])null);
            declaringType = innerDeclaringType;
            return result;
        }

        internal ObjectIdentityHandle[] GetTypeGroupEventInvokeArgs(ObjectIdentityHandle typeGroup)
        {
            return CallAndHandle(() =>
            {
                var self = (TypeGroup)Unwrap(typeGroup);
                foreach (var type in self.Types)
                {
                    if (typeof(Delegate).IsAssignableFrom(type))
                    {
                        return GetEventInvokeArgs(type);
                    }
                }
                return null;
            }, (ObjectIdentityHandle[])null);
        }

        #endregion

        #region ReflectedExtensionProperty Helpers

        internal ObjectIdentityHandle GetExtensionPropertyType(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var property = (ReflectedExtensionProperty)Unwrap(value);
                return MakeHandle(property.PropertyType);
            });
        }

        internal string GetExtensionPropertyDocumentation(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var property = (ReflectedExtensionProperty)Unwrap(value);
                try
                {
                    return property.__doc__;
                }
                catch (ArgumentException)
                {
                    // IronPython can throw here if it can't figure out the 
                    // path of the assembly this type is defined in.
                    return "";
                }
            });
        }

        #endregion

        internal PythonType GetTypeFromType(Type type)
        {
            if (type.IsGenericParameter && type.GetInterfaces().Length != 0)
            {
                // generic parameter with constraints, IronPython will throw an 
                // exception while constructing the PythonType 
                // http://ironpython.codeplex.com/workitem/30905
                // Return the type for the interface
                return GetTypeFromType(type.GetInterfaces()[0]);
            }

            return DynamicHelpers.GetPythonTypeFromType(type);
        }

        internal ObjectIdentityHandle GetBuiltinType(BuiltinTypeId id)
        {
            return CallAndHandle(() =>
            {
                switch (id)
                {
                    case BuiltinTypeId.Bool: return MakeHandle(GetTypeFromType(typeof(bool)));
                    case BuiltinTypeId.BuiltinFunction: return MakeHandle(GetTypeFromType(typeof(BuiltinFunction)));
                    case BuiltinTypeId.BuiltinMethodDescriptor: return MakeHandle(GetTypeFromType(typeof(BuiltinMethodDescriptor)));
                    case BuiltinTypeId.Complex: return MakeHandle(GetTypeFromType(typeof(Complex)));
                    case BuiltinTypeId.Dict: return MakeHandle(GetTypeFromType(typeof(PythonDictionary)));
                    case BuiltinTypeId.Float: return MakeHandle(GetTypeFromType(typeof(double)));
                    case BuiltinTypeId.Function: return MakeHandle(GetTypeFromType(typeof(PythonFunction)));
                    case BuiltinTypeId.Generator: return MakeHandle(GetTypeFromType(typeof(PythonGenerator)));
                    case BuiltinTypeId.Int: return MakeHandle(GetTypeFromType(typeof(int)));
                    case BuiltinTypeId.List: return MakeHandle(GetTypeFromType(typeof(List)));
                    case BuiltinTypeId.Long: return MakeHandle(GetTypeFromType(typeof(System.Numerics.BigInteger)));
                    case BuiltinTypeId.Unknown: return MakeHandle(GetTypeFromType(typeof(DynamicNull)));
                    case BuiltinTypeId.Object: return MakeHandle(GetTypeFromType(typeof(object)));
                    case BuiltinTypeId.Set: return MakeHandle(GetTypeFromType(typeof(SetCollection)));
                    case BuiltinTypeId.FrozenSet: return MakeHandle(GetTypeFromType(typeof(FrozenSetCollection)));
                    case BuiltinTypeId.Str: return MakeHandle(GetTypeFromType(typeof(string)));
                    case BuiltinTypeId.Unicode: return MakeHandle(GetTypeFromType(typeof(string)));
                    case BuiltinTypeId.Bytes: return MakeHandle(GetTypeFromType(typeof(string)));   // keep strings and bytes the same on Ipy because '' and u'abc' create the same type
                    case BuiltinTypeId.Tuple: return MakeHandle(GetTypeFromType(typeof(PythonTuple)));
                    case BuiltinTypeId.Type: return MakeHandle(GetTypeFromType(typeof(PythonType)));
                    case BuiltinTypeId.NoneType: return MakeHandle(GetTypeFromType(typeof(DynamicNull)));
                    case BuiltinTypeId.Ellipsis: return MakeHandle(GetTypeFromType(typeof(Ellipsis)));
                    case BuiltinTypeId.DictKeys: return MakeHandle(GetTypeFromType(typeof(DictionaryKeyEnumerator)));
                    case BuiltinTypeId.DictValues: return MakeHandle(GetTypeFromType(typeof(DictionaryValueEnumerator)));
                    case BuiltinTypeId.DictItems: return MakeHandle(GetTypeFromType(typeof(DictionaryItemEnumerator)));
                    case BuiltinTypeId.Module: return MakeHandle(GetTypeFromType(typeof(PythonModule)));
                    case BuiltinTypeId.ListIterator: return MakeHandle(GetTypeFromType(typeof(ListIterator)));
                    case BuiltinTypeId.TupleIterator: return MakeHandle(GetTypeFromType(typeof(TupleEnumerator)));
                    case BuiltinTypeId.SetIterator: return MakeHandle(GetTypeFromType(typeof(SetIterator)));
                    case BuiltinTypeId.StrIterator: return MakeHandle(GetTypeFromType(typeof(IEnumeratorOfTWrapper<string>)));
                    case BuiltinTypeId.BytesIterator: return MakeHandle(GetTypeFromType(typeof(IEnumeratorOfTWrapper<string>)));
                    case BuiltinTypeId.UnicodeIterator: return MakeHandle(GetTypeFromType(typeof(IEnumeratorOfTWrapper<string>)));
                    case BuiltinTypeId.CallableIterator: return MakeHandle(GetTypeFromType(typeof(SentinelIterator)));
                    case BuiltinTypeId.Property: return MakeHandle(GetTypeFromType(typeof(PythonProperty)));
                    case BuiltinTypeId.ClassMethod: return MakeHandle(GetTypeFromType(typeof(classmethod)));
                    case BuiltinTypeId.StaticMethod: return MakeHandle(GetTypeFromType(typeof(staticmethod)));
                    default:
                        return new ObjectIdentityHandle();
                }
            });
        }

        #region MethodBase Helpers 

        internal ObjectIdentityHandle GetBuiltinFunctionOverloadReturnType(ObjectIdentityHandle value)
        {
            return CallAndHandle(() =>
            {
                var overload = (MethodBase)Unwrap(value);

                MethodInfo mi = overload as MethodInfo;
                if (mi != null)
                {
                    return MakeHandle(GetTypeFromType(mi.ReturnType));
                }

                return MakeHandle(GetTypeFromType(overload.DeclaringType));
            });
        }

        internal bool IsInstanceExtensionMethod(ObjectIdentityHandle methodBase, ObjectIdentityHandle declaringType)
        {
            return CallAndHandle(() =>
            {
                var target = (MethodBase)Unwrap(methodBase);
                var type = (PythonType)Unwrap(declaringType);

                bool isInstanceExtensionMethod = false;
                if (!target.DeclaringType.IsAssignableFrom(type.__clrtype__()))
                {
                    isInstanceExtensionMethod = !target.IsDefined(typeof(StaticExtensionMethodAttribute), false);
                }
                return isInstanceExtensionMethod;
            }, false);
        }

        internal ObjectIdentityHandle[] GetParametersNoCodeContext(ObjectIdentityHandle methodBase)
        {
            return CallAndHandle(() =>
            {
                var target = (MethodBase)Unwrap(methodBase);

                var parameters = target.GetParameters();
                var res = new List<ObjectIdentityHandle>(parameters.Length);
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (res.Count == 0 && parameters[i].ParameterType.FullName == _codeCtxType)
                    {
                        // skip CodeContext variable
                        continue;
                    }
                    res.Add(MakeHandle(parameters[i]));
                }
                return res.ToArray();
            }, () => new ObjectIdentityHandle[0]);
        }

        #endregion

        #region ParameterInfo Helpers

        internal string GetParameterName(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var parameterInfo = (ParameterInfo)Unwrap(handle);

                return parameterInfo.Name;
            });
        }

        internal ParameterKind GetParameterKind(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var parameterInfo = (ParameterInfo)Unwrap(handle);

                if (parameterInfo.IsDefined(typeof(ParamArrayAttribute), false))
                {
                    return ParameterKind.List;
                }
                else if (parameterInfo.IsDefined(typeof(ParamDictionaryAttribute), false))
                {
                    return ParameterKind.Dictionary;
                }
                return ParameterKind.Normal;
            }, ParameterKind.Normal);
        }

        internal ObjectIdentityHandle GetParameterPythonType(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var parameterInfo = (ParameterInfo)Unwrap(handle);

                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(parameterInfo.ParameterType));
            });
        }

        internal string GetParameterDefaultValue(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                var parameterInfo = (ParameterInfo)Unwrap(handle);

                if (parameterInfo.DefaultValue != DBNull.Value && !(parameterInfo.DefaultValue is Missing))
                {
                    return PythonOps.Repr(_codeContext, parameterInfo.DefaultValue);
                }
                else if (parameterInfo.IsOptional)
                {
                    object missing = CompilerHelpers.GetMissingValue(parameterInfo.ParameterType);
                    if (missing != Missing.Value)
                    {
                        return PythonOps.Repr(_codeContext, missing);
                    }
                    else
                    {
                        return "";
                    }
                }
                return null;
            });
        }

        #endregion

        #region ConstructorInfo Helpers

        internal ObjectIdentityHandle GetConstructorDeclaringPythonType(ObjectIdentityHandle ctor)
        {
            return CallAndHandle(() =>
            {
                var method = (MethodBase)Unwrap(ctor);

                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(method.DeclaringType));
            });
        }

        #endregion

        /// <summary>
        /// Used for assertions only, making sure we're constructing things w/ the correct types.
        /// </summary>
        internal bool TypeIs<T>(ObjectIdentityHandle handle)
        {
            return CallAndHandle(() =>
            {
                return Unwrap(handle) is T;
            }, false);
        }

        /// <summary>
        /// Sets the current list of analysis directories.  Returns true if the list changed and the
        /// module changed event should be raised to the analysis engine.
        /// </summary>
        internal SetAnalysisDirectoriesResult SetAnalysisDirectories(string[] dirs)
        {
            return CallAndHandle(() =>
            {
                SetAnalysisDirectoriesResult raiseModuleChangedEvent = SetAnalysisDirectoriesResult.NoChange;
                if (_analysisDirs != null)
                {
                    // check if we're removing any dirs, and if we are, re-initialize our namespace object...
                    var newDirs = new HashSet<string>(dirs, StringComparer.OrdinalIgnoreCase);
                    foreach (var dir in _analysisDirs)
                    {
                        lock (_assembliesLoadedFromDirectories)
                        {
                            if (!newDirs.Contains(dir) && _assembliesLoadedFromDirectories.Contains(dir))
                            {
                                // this directory was removed                        
                                raiseModuleChangedEvent = SetAnalysisDirectoriesResult.Reload;
                                _assembliesLoadedFromDirectories.Remove(dir);
                            }
                        }
                    }

                    // check if we're adding new dirs, in which case we need to raise the modules change event
                    // in IronPythonInterpreter so that we'll re-analyze all of the files and we can pick up
                    // the new assemblies.
                    if (raiseModuleChangedEvent == SetAnalysisDirectoriesResult.NoChange)
                    {
                        HashSet<string> existing = new HashSet<string>(_analysisDirs);
                        foreach (var dir in dirs)
                        {
                            if (!existing.Contains(dir))
                            {
                                raiseModuleChangedEvent = SetAnalysisDirectoriesResult.ModulesChanged;
                                break;
                            }
                        }
                    }
                }
                else if (dirs.Length > 0)
                {
                    raiseModuleChangedEvent = SetAnalysisDirectoriesResult.ModulesChanged;
                }

                _analysisDirs = dirs;
                return raiseModuleChangedEvent;
            }, SetAnalysisDirectoriesResult.NoChange);
        }

        internal bool LoadAssemblyReference(string assembly)
        {
            try
            {
                byte[] symbolStore = null;
                try
                {
                    // If PTVS is being debugged, then that debugger will lock
                    // the pdb of the assembly we are loading here.  This is a
                    // problem that PTVS developers will see, but PTVS users shouldn't.
                    // Loading the symbol store and passing it in prevents this locking.
                    string symbolStorePath = Path.ChangeExtension(assembly, ".pdb");
                    if (File.Exists(symbolStorePath))
                    {
                        symbolStore = File.ReadAllBytes(symbolStorePath);
                    }
                }
                catch
                {
                }
                var asm = Assembly.Load(File.ReadAllBytes(assembly), symbolStore);
                if (asm != null)
                {
                    _referencedAssemblies[asm.FullName] = asm;
                    _referencedAssemblies[new AssemblyName(asm.FullName).Name] = asm;
                    _referencedAssemblies[assembly] = asm;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        internal bool LoadAssemblyReferenceByName(string assemblyName)
        {
            try
            {
                var asm = Assembly.Load(assemblyName);
                if (asm != null)
                {
                    _referencedAssemblies[asm.FullName] = asm;
                    _referencedAssemblies[new AssemblyName(asm.FullName).Name] = asm;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        internal bool UnloadAssemblyReference(string name)
        {
            return CallAndHandle(() =>
            {
                return _referencedAssemblies.ContainsKey(name);
            }, false);
        }

        internal ObjectIdentityHandle GetBuiltinTypeFromType(Type type)
        {
            return CallAndHandle(() =>
            {
                return MakeHandle(DynamicHelpers.GetPythonTypeFromType(type));
            });
        }
    }

    enum ParameterKind
    {
        Unknown,
        Normal,
        List,
        Dictionary
    }

    enum ObjectKind
    {
        None,
        Module,
        Type,
        ClrType,
        BuiltinFunction,
        BuiltinMethodDesc,
        ReflectedField,
        ReflectedProperty,
        ReflectedExtensionProperty,
        NamespaceTracker,
        Method,
        ClassMethod,
        PythonTypeTypeSlot,
        ReflectedEvent,
        PythonTypeSlot,
        TypeGroup,
        Constant,
        ConstructorFunction,
        Unknown,
    }

    enum SetAnalysisDirectoriesResult
    {
        NoChange,
        Reload,
        ModulesChanged

    }
}
