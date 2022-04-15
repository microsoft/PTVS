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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Debugger;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class PyTypeAttribute : Attribute {
        public string VariableName { get; set; }
        public PythonLanguageVersion MinVersion { get; set; }
        public PythonLanguageVersion MaxVersion { get; set; }
    }

    internal interface IPyObject : IValueStore<PyObject>, IDataProxy<StructProxy> {
        PointerProxy<PyTypeObject> ob_type { get; }
    }

    [PyType(VariableName = "PyBaseObject_Type")]
    internal class PyObject : StructProxy, IPyObject {
        private struct ProxyInfo {
            public readonly Type ProxyType;
            public readonly Func<DkmProcess, ulong, PyObject> ProxyFactory;

            public static readonly ProxyInfo Default = new ProxyInfo(typeof(PyObject));

            public ProxyInfo(Type proxyType) {
                ProxyType = proxyType;

                var ctor = proxyType.GetConstructor(new[] { typeof(DkmProcess), typeof(ulong) });
                if (ctor == null) {
                    Debug.Fail("PyObject-derived type " + proxyType.Name + " does not have a (DkmProcess, ulong) constructor.");
                    throw new NotSupportedException();
                }

                var process = Expression.Parameter(typeof(DkmProcess));
                var address = Expression.Parameter(typeof(ulong));
                ProxyFactory = Expression.Lambda<Func<DkmProcess, ulong, PyObject>>(Expression.New(ctor, process, address), process, address).Compile();
            }
        }

        private class ProxyTypes : DkmDataItem {
            public readonly Dictionary<ulong, ProxyInfo> ProxyInfoFromPyTypePtr = new Dictionary<ulong, ProxyInfo>();
            public readonly Dictionary<Type, PyTypeObject> PyTypeFromType = new Dictionary<Type, PyTypeObject>();

            public ProxyTypes(DkmProcess process) {
                var langVer = process.GetPythonRuntimeInfo().LanguageVersion;

                var proxyTypes = typeof(PyObject).Assembly.GetTypes().Where(t => typeof(PyObject).IsAssignableFrom(t) && !t.IsAbstract);
                foreach (var proxyType in proxyTypes) {
                    string typeVarName = null;

                    var pyTypeAttrs = (PyTypeAttribute[])Attribute.GetCustomAttributes(proxyType, typeof(PyTypeAttribute), inherit: false);
                    if (pyTypeAttrs.Length == 0) {
                        typeVarName = ComputeVariableName(proxyType);
                    } else {
                        foreach (var pyTypeAttr in pyTypeAttrs) {
                            if (pyTypeAttr.MinVersion != PythonLanguageVersion.None && langVer < pyTypeAttr.MinVersion) {
                                continue;
                            }
                            if (pyTypeAttr.MaxVersion != PythonLanguageVersion.None && langVer > pyTypeAttr.MaxVersion) {
                                continue;
                            }

                            typeVarName = pyTypeAttr.VariableName ?? ComputeVariableName(proxyType);
                            break;
                        }

                        if (typeVarName == null) {
                            continue;
                        }
                    }

                    var pyType = PyTypeObject.FromNativeGlobalVariable(process, typeVarName);
                    var proxyInfo = new ProxyInfo(proxyType);
                    ProxyInfoFromPyTypePtr.Add(pyType.Address, proxyInfo);

                    PyTypeFromType.Add(proxyType, pyType);
                }
            }

            private static string ComputeVariableName(Type proxyType) {
                if (!proxyType.Name.EndsWithOrdinal("Object")) {
                    Debug.Fail("PyObject-derived type " + proxyType.Name + " must have name ending with 'Object' to infer type variable name.");
                    throw new NotSupportedException();
                }

                return proxyType.Name.Substring(0, proxyType.Name.Length - 6) + "_Type";
            }
        }

        internal class PyObject_Fields {
            public StructField<SSizeTProxy> ob_refcnt;
            public StructField<PointerProxy> ob_type;
        }

        private readonly PyObject_Fields _fields;

        public PyObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        PyObject IValueStore<PyObject>.Read() {
            return this;
        }

        object IValueStore.Read() {
            return this;
        }

        [Conditional("DEBUG")]
        protected void CheckPyType<TObject>() where TObject : PyObject  {
            // Check whether this is a freshly allocated object and skip the type check if so.
            if (ob_refcnt.Read() == 0) {
                return;
            }

            var proxyTypes = Process.GetOrCreateDataItem(() => new ProxyTypes(Process));
            var expectedType = proxyTypes.PyTypeFromType[typeof(TObject)];
            var ob_type = this.ob_type.Read();
            if (!ob_type.IsSubtypeOf(expectedType)) {
                Debug.Fail("Expected object of type " + expectedType.tp_name.Read().ReadUnicode() + " but got a " + ob_type.tp_name.Read().ReadUnicode() + " instead.");
                throw new InvalidOperationException();
            }
        }

        public SSizeTProxy ob_refcnt {
            get { return GetFieldProxy(_fields.ob_refcnt); }
        }

        public PointerProxy<PyTypeObject> ob_type {
            get {
                // PyObject::ob_type is declared as PyTypeObject*, so layout of that struct is always valid and applicable;
                // but ob_type->ob_type is not always PyTypeObject or a derived type (native modules can play shenanigans with
                // metaclasses that way, and ctypes in particular does that). Since polymorphic creation relies on ob_type,
                // skip here, and just return a PyTypeObject. We don't have any derived proxies to worry about, anyway.
                return GetFieldProxy(_fields.ob_type).ReinterpretCast<PyTypeObject>(polymorphic: false);
            }
        }

        public PointerProxy<PyObject>? __dict__ {
            get {
                var ob_type = this.ob_type.Read();
                long dictoffset = ob_type.tp_dictoffset.Read();

                if (dictoffset == 0) {
                    return null;
                } else if (dictoffset < 0) {
                    var varObj = this as PyVarObject;
                    if (varObj == null) {
                        throw new InvalidDataException();
                    }

                    long size = ob_type.tp_basicsize.Read();
                    size += checked(Math.Abs(varObj.ob_size.Read()) * ob_type.tp_itemsize.Read());

                    // Align to pointer boundary
                    var sizeofPtr = Process.GetPointerSize();
                    size += (sizeofPtr - 1);
                    size &= ~(sizeofPtr - 1);

                    dictoffset += size;
                    Debug.Assert(dictoffset > 0);
                }

                return new PointerProxy<PyObject>(Process, Address.OffsetBy(dictoffset));
            }
        }

        public bool IsInstanceOf(PyTypeObject type) {
            if (this == type) {
                return true;
            }
            return ob_type.Read().IsSubtypeOf(type);
        }

        public override string ToString() {
            var pyrtInfo = Process.GetPythonRuntimeInfo();
            var reprBuilder = new ReprBuilder(new ReprOptions(Process));
            Repr(reprBuilder);
            return reprBuilder.ToString();
        }

        /// <summary>
        /// Appends a readable representation of the object to be shown in various debugger windows (Locals, Watch etc) of this object to <paramref name="builder"/>.
        /// </summary>
        public virtual void Repr(ReprBuilder builder) {
            if (this == None(Process)) {
                builder.Append("None");
            } else {
                builder.AppendFormat("<{0} object at {1:PTR}>", ob_type.Read().tp_name.Read().ReadUnicode(), Address);
            }
        }

        /// <summary>
        /// Returns a readable representation of the object to be shown in various debugger windows (Locals, Watch etc).
        /// </summary>
        public string Repr(ReprOptions options) {
            var builder = new ReprBuilder(options);
            Repr(builder);
            return builder.ToString();
        }

        /// <summary>
        /// Returns the sequence of results to display as children of this object in various debugger data views (Locals window etc).
        /// Unnamed results are displayed as [0], [1], ....
        /// </summary>
        /// <remarks>
        /// The default implementation enumerates object fields specified via either __dict__ or __slots__. Most derived classes
        /// will want to keep that as is, except for collections.
        /// </remarks>
        public virtual IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            var children = GetDebugChildrenFromSlots();

            var maybeDictProxy = this.__dict__;
            if (maybeDictProxy != null) {
                var dictProxy = maybeDictProxy.Value;
                if (!dictProxy.IsNull) {
                    yield return new PythonEvaluationResult(dictProxy, "__dict__");
                    var dict = dictProxy.TryRead() as PyDictObject;
                    if (dict != null) {
                        children = children.Concat(GetDebugChildrenFromDict(dict));
                    }
                }
            }

            children = children.OrderBy(pair => pair.Name);
            foreach (var pair in children) {
                yield return pair;
            }
        }

        private IEnumerable<PythonEvaluationResult> GetDebugChildrenFromDict(PyDictObject dict) {
            foreach (var pair in dict.ReadElements()) {
                var name = pair.Key as IPyBaseStringObject;
                if (name != null && !pair.Value.IsNull) {
                    yield return new PythonEvaluationResult(pair.Value, name.ToString());
                }
            }
        }

        private IEnumerable<PythonEvaluationResult> GetDebugChildrenFromSlots() {
            var tp_members = ob_type.Read().tp_members;
            if (tp_members.IsNull) {
                yield break;
            }

            var langVer = Process.GetPythonRuntimeInfo().LanguageVersion;

            var memberDefs = tp_members.Read().TakeWhile(md => !md.name.IsNull);
            foreach (PyMemberDef memberDef in memberDefs) {
                var offset = memberDef.offset.Read();
                IValueStore value;
                switch (memberDef.type.Read()) {
                    case PyMemberDefType.T_OBJECT:
                    case PyMemberDefType.T_OBJECT_EX:
                        {
                            var objProxy = GetFieldProxy(new StructField<PointerProxy<PyObject>> { Process = Process, Offset = offset });
                            if (!objProxy.IsNull) {
                                value = objProxy;
                            } else {
                                value = new ValueStore<PyObject>(None(Process));
                            }
                        } break;
                    case PyMemberDefType.T_STRING:
                        {
                            var ptr = GetFieldProxy(new StructField<PointerProxy> { Process = Process, Offset = offset }).Read();
                            if (ptr != 0) {
                                var proxy = new CStringProxy(Process, ptr);
                                if (langVer <= PythonLanguageVersion.V27) {
                                    value = new ValueStore<AsciiString>(proxy.ReadAscii());
                                } else {
                                    value = new ValueStore<string>(proxy.ReadUnicode());
                                }
                            } else {
                                value = new ValueStore<PyObject>(None(Process));
                            }
                        } break;
                    case PyMemberDefType.T_STRING_INPLACE: {
                            var proxy = new CStringProxy(Process, Address.OffsetBy(offset));
                            if (langVer <= PythonLanguageVersion.V27) {
                                value = new ValueStore<AsciiString>(proxy.ReadAscii());
                            } else {
                                value = new ValueStore<string>(proxy.ReadUnicode());
                            }
                        } break;
                    case PyMemberDefType.T_BYTE:
                        value = GetFieldProxy(new StructField<SByteProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_UBYTE:
                        value = GetFieldProxy(new StructField<ByteProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_SHORT:
                        value = GetFieldProxy(new StructField<Int16Proxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_USHORT:
                        value = GetFieldProxy(new StructField<UInt16Proxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_INT:
                    case PyMemberDefType.T_LONG:
                        value = GetFieldProxy(new StructField<Int32Proxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_UINT:
                    case PyMemberDefType.T_ULONG:
                        value = GetFieldProxy(new StructField<UInt32Proxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_LONGLONG:
                        value = GetFieldProxy(new StructField<Int64Proxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_ULONGLONG:
                        value = GetFieldProxy(new StructField<UInt64Proxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_PYSSIZET:
                        value = GetFieldProxy(new StructField<SSizeTProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_FLOAT:
                        value = GetFieldProxy(new StructField<SingleProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_DOUBLE:
                        value = GetFieldProxy(new StructField<DoubleProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_BOOL:
                        value = GetFieldProxy(new StructField<BoolProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_CHAR:
                        value = GetFieldProxy(new StructField<CharProxy> { Process = Process, Offset = offset });
                        break;
                    case PyMemberDefType.T_NONE:
                        value = new ValueStore<PyObject>(None(Process));
                        break;
                    default:
                        continue;
                }

                yield return new PythonEvaluationResult(value, memberDef.name.Read().ReadUnicode());
            }
        }

        private class NoneHolder : DkmDataItem {
            public readonly PyObject None;

            public NoneHolder(DkmProcess process) {
                None = new PyObject(process, process.GetPythonRuntimeInfo().DLLs.Python.GetStaticVariableAddress("_Py_NoneStruct"));
            }
        }

        public static PyObject None(DkmProcess process) {
            return process.GetOrCreateDataItem(() => new NoneHolder(process)).None;
        }

        public bool IsNone {
            get { return this == None(Process); }
        }

        public static PyObject FromAddress(DkmProcess process, ulong address) {
            if (address == 0) {
                return null;
            }

            // This is a hot code path, so avoid creating a PyTypeObject here just to check for reference equality -
            // read it as raw pointer, and do direct address comparisons instead.
            var fields = GetStructFields<PyObject, PyObject_Fields>(process);
            ulong typePtr = new PointerProxy(process, address.OffsetBy(fields.ob_type.Offset)).Read();
            var proxyInfo = FindProxyInfoForPyType(process, typePtr) ?? ProxyInfo.Default;
            return proxyInfo.ProxyFactory(process, address);
        }

        private static ProxyInfo? FindProxyInfoForPyType(DkmProcess process, ulong typePtr) {
            if (typePtr == 0) {
                return null;
            }

            ProxyInfo proxyInfo;
            var map = process.GetOrCreateDataItem(() => new ProxyTypes(process)).ProxyInfoFromPyTypePtr;
            if (map.TryGetValue(typePtr, out proxyInfo)) {
                return proxyInfo;
            }

            // If we didn't get a direct match, look at tp_base and tp_bases.
            var typeObject = new PyTypeObject(process, typePtr);
            var tp_base = typeObject.tp_base.Raw.Read();
            var baseProxyInfo = FindProxyInfoForPyType(process, tp_base);
            if (baseProxyInfo != null) {
                return baseProxyInfo;
            }

            var tp_bases = typeObject.tp_bases.TryRead();
            if (tp_bases != null) {
                foreach (var bas in tp_bases.ReadElements()) {
                    baseProxyInfo = FindProxyInfoForPyType(process, bas.Raw.Read());
                    if (baseProxyInfo != null) {
                        return baseProxyInfo;
                    }
                }
            }

            return null;
        }

        public static PyTypeObject GetPyType<TObject>(DkmProcess process)
            where TObject : PyObject {
            var map = process.GetOrCreateDataItem(() => new ProxyTypes(process)).PyTypeFromType;
            return map[typeof(TObject)];
        }
    }

    internal abstract class PyVarObject : PyObject {
        internal class PyVarObject_Fields {
            public StructField<SSizeTProxy> ob_size;
        }

        private readonly PyVarObject_Fields _fields;

        public PyVarObject(DkmProcess process, ulong address)
            : base(process, address) {
            InitializeStruct(this, out _fields);
        }

        public SSizeTProxy ob_size {
            get {
                return GetFieldProxy(_fields.ob_size);
            }
        }
    }
}
