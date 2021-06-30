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
using System.Diagnostics;
using System.Linq;
using Microsoft.Dia;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    internal class StructProxyAttribute : Attribute {
        public string StructName { get; set; }
        public PythonLanguageVersion MinVersion { get; set; }
        public PythonLanguageVersion MaxVersion { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    internal class FieldProxyAttribute : Attribute {
        public string FieldName { get; set; }
        public PythonLanguageVersion MinVersion { get; set; }
        public PythonLanguageVersion MaxVersion { get; set; }
    }

    // Represents a reference to a struct inside the debuggee process.
    internal abstract class StructProxy : IDataProxy<StructProxy> {

        public class StructMetadata : DkmDataItem {
            public DkmProcess Process { get; private set; }
            public string Name { get; private set; }
            public long Size { get; private set; }
            internal object Fields { get; set; }
            private readonly ComPtr<IDiaSymbol> _symbol;

            public StructMetadata(DkmProcess process, string name) {
                Process = process;
                Name = name;

                using (var pythonSymbols = process.GetPythonRuntimeInfo().DLLs.Python.GetSymbols()) {
                    _symbol = pythonSymbols.Object.GetTypeSymbol(name);
                }
                Size = (long)Symbol.length;
            }

            public IDiaSymbol Symbol {
                get { return _symbol.Object; }
            }

            protected override void OnClose() {
                _symbol.Dispose();
                base.OnClose();
            }
        }

        private class StructMetadata<TStruct> : StructMetadata
            where TStruct : StructProxy {

            public StructMetadata(DkmProcess process)
                : base(process, GetName(process)) {
            }

            private static string GetName(DkmProcess process) {
                var langVer = process.GetPythonRuntimeInfo().LanguageVersion;

                var proxyAttrs = (StructProxyAttribute[])Attribute.GetCustomAttributes(typeof(TStruct), typeof(StructProxyAttribute));
                if (proxyAttrs.Length == 0) {
                    return typeof(TStruct).Name;
                } else {
                    foreach (var proxyAttr in proxyAttrs) {
                        if (proxyAttr.MinVersion != PythonLanguageVersion.None && langVer < proxyAttr.MinVersion) {
                            continue;
                        }
                        if (proxyAttr.MaxVersion != PythonLanguageVersion.None && langVer > proxyAttr.MaxVersion) {
                            continue;
                        }

                        return proxyAttr.StructName ?? typeof(TStruct).Name;
                    }

                    Debug.Fail("Failed to obtain metadata for type " + typeof(TStruct).Name + " because it is not available in " + langVer);
                    throw new InvalidOperationException();
                }
            }
        }

        private readonly DkmProcess _process;
        private readonly ulong _address;
        private StructMetadata _metadata;

        protected StructProxy(DkmProcess process, ulong address) {
            Debug.Assert(process != null && address != 0);
            _process = process;
            _address = address;
        }

        public DkmProcess Process {
            get { return _process; }
        }

        public ulong Address {
            get { return _address; }
        }

        public long ObjectSize {
            get { return _metadata.Size; }
        }

        StructProxy IValueStore<StructProxy>.Read() {
            return this;
        }

        object IValueStore.Read() {
            return this;
        }

        public override bool Equals(object obj) {
            var other = obj as StructProxy;
            if (other == null) {
                return false;
            }
            return this == other;
        }

        public override int GetHashCode() {
            return new { _process, _address }.GetHashCode();
        }

        protected TProxy GetFieldProxy<TProxy>(StructField<TProxy>? field, bool polymorphic = true)
            where TProxy : IDataProxy {
            return field.HasValue ? GetFieldProxy(field.Value) : default(TProxy);
        }

        protected TProxy GetFieldProxy<TProxy>(StructField<TProxy> field, bool polymorphic = true)
            where TProxy : IDataProxy {
            return DataProxy.Create<TProxy>(Process, Address.OffsetBy(field.Offset), polymorphic);
        }

        public static bool operator ==(StructProxy lhs, StructProxy rhs) {
            if ((object)lhs == null) {
                if ((object)rhs == null) {
                    return true;
                } else {
                    return rhs._address == 0;
                }
            } else if ((object)rhs == null) {
                return lhs._address == 0;
            } else {
                return lhs._process == rhs._process && lhs._address == rhs._address;
            }
        }

        public static bool operator !=(StructProxy lhs, StructProxy rhs) {
            return !(lhs == rhs);
        }

        public static StructMetadata GetStructMetadata<TStruct>(DkmProcess process)
            where TStruct : StructProxy {

            var metadata = process.GetDataItem<StructMetadata<TStruct>>();
            if (metadata != null) {
                return metadata;
            }

            metadata = new StructMetadata<TStruct>(process);
            process.SetDataItem(DkmDataCreationDisposition.CreateNew, metadata);
            return metadata;
        }

        private static string GetFieldName(System.Reflection.FieldInfo fieldInfo, PythonLanguageVersion pyVersion) {
            string name = fieldInfo.Name;
            foreach (var attr in Attribute.GetCustomAttributes(fieldInfo, typeof(FieldProxyAttribute)).OfType<FieldProxyAttribute>()) {
                name = null;
                if ((attr.MinVersion.IsNone() || pyVersion >= attr.MinVersion) &&
                    (attr.MaxVersion.IsNone() || pyVersion <= attr.MaxVersion)) {
                    return string.IsNullOrEmpty(attr.FieldName) ? fieldInfo.Name : attr.FieldName;
                }
            }
            return name;
        }

        private static TFields GetStructFields<TFields>(StructMetadata metadata)
            where TFields : class, new() {

            if (metadata.Fields != null) {
                return (TFields)metadata.Fields;
            }

            var pyVersion = metadata.Process.GetPythonRuntimeInfo().LanguageVersion;

            var fields = new TFields();
            foreach (var fieldInfo in typeof(TFields).GetFields()) {
                var fieldType = fieldInfo.FieldType;
                if (fieldType.GetInterfaces().Contains(typeof(IStructField))) {
                    Debug.Assert(!fieldInfo.IsInitOnly);

                    var name = GetFieldName(fieldInfo, pyVersion);
                    if (string.IsNullOrEmpty(name)) {
                        continue;
                    }

                    long offset = metadata.Symbol.GetFieldOffset(name);
                    var field = (IStructField)Activator.CreateInstance(fieldType);
                    field.Process = metadata.Process;
                    field.Offset = offset;
                    fieldInfo.SetValue(fields, field);
                }
            }

            metadata.Fields = fields;
            return fields;
        }

        public static TFields GetStructFields<TStruct, TFields>(DkmProcess process)
            where TStruct : StructProxy
            where TFields : class, new() {
            var metadata = GetStructMetadata<TStruct>(process);
            return GetStructFields<TFields>(metadata);
        }

        protected void InitializeStruct<TStruct, TFields>(TStruct this_, out TFields fields)
            where TStruct : StructProxy
            where TFields : class, new() {

            Debug.Assert(this == this_);

            var metadata = GetStructMetadata<TStruct>(Process);
            fields = GetStructFields<TFields>(metadata);
            _metadata = metadata;
        }

        public static long SizeOf<TStruct>(DkmProcess process)
            where TStruct : StructProxy {
            return GetStructMetadata<TStruct>(process).Size;
        }
    }

    internal interface IStructField {
        DkmProcess Process { get; set; }
        long Offset { get; set; }
    }

    internal struct StructField<TProxy> : IStructField
        where TProxy : IDataProxy {
        public DkmProcess Process { get; set; }
        public long Offset { get; set; }
    }
}
