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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class PythonObject : IMemberContainer, IMember {
        private readonly ObjectIdentityHandle _obj;
        private readonly IronPythonInterpreter _interpreter;
        private RemoteInterpreterProxy _remote;
        private Dictionary<string, MemberInfo> _attrs;
        private bool _checkedClrAttrs;

        public PythonObject(IronPythonInterpreter interpreter, ObjectIdentityHandle obj) {
            _interpreter = interpreter;
            _interpreter.UnloadingDomain += Interpreter_UnloadingDomain;
            _remote = _interpreter.Remote;
            _obj = obj;
        }

        private void Interpreter_UnloadingDomain(object sender, EventArgs e) {
            _remote = null;
            _interpreter.UnloadingDomain -= Interpreter_UnloadingDomain;
        }

        public ObjectIdentityHandle Value {
            get {
                return _obj;
            }
        }

        public IronPythonInterpreter Interpreter {
            get {
                return _interpreter;
            }
        }

        public RemoteInterpreterProxy RemoteInterpreter {
            get {
                return _remote;
            }
        }

        struct MemberInfo {
            public readonly IMember Member;
            public IsClrOnly ClrOnly;

            public MemberInfo(IMember member) {
                Member = member;
                ClrOnly = IsClrOnly.NotChecked;
            }

            public MemberInfo(IsClrOnly isClrOnly) {
                ClrOnly = isClrOnly;
                Member = null;
            }

            public MemberInfo(IMember memberInfo, IsClrOnly isClrOnly) {
                Member = memberInfo;
                ClrOnly = isClrOnly;
            }
        }

        enum IsClrOnly {
            NotChecked,
            Yes,
            No
        }

        #region IMemberContainer Members

        public IMember GetMember(IModuleContext context, string name) {
            if (_attrs == null) {
                Interlocked.CompareExchange(ref _attrs, new Dictionary<string, MemberInfo>(), null);
            }
            bool showClr = context == null || ((IronPythonModuleContext)context).ShowClr;

            MemberInfo member;
            if (!_attrs.TryGetValue(name, out member) || member.Member == null) {
                var ri = RemoteInterpreter;
                var res = ri != null ? ri.GetMember(Value, name) : default(ObjectIdentityHandle);
                if (!res.Equals(Value)) {
                    _attrs[name] = member = new MemberInfo(_interpreter.MakeObject(res));
                }
            }

            if (!showClr) {
                if (!(this is IronPythonNamespace)) {   // namespaces always show all of their members...
                    switch (member.ClrOnly) {
                        case IsClrOnly.NotChecked:
                            CreateNonClrAttrs();
                            if (_attrs.ContainsKey(name) &&
                                _attrs[name].ClrOnly == IsClrOnly.Yes) {
                                return null;
                            }
                            break;
                        case IsClrOnly.No:
                            break;
                        case IsClrOnly.Yes:
                            return null;
                    }
                }
            }

            return member.Member;
        }

        private void CreateNonClrAttrs() {
            if (!_checkedClrAttrs) {
                var ri = RemoteInterpreter;
                foreach (var name in ri != null ? ri.DirHelper(_obj, false) : Enumerable.Empty<string>()) {
                    if (!_attrs.ContainsKey(name)) {
                        _attrs[name] = new MemberInfo(IsClrOnly.No);
                    } else {
                        _attrs[name] = new MemberInfo(_attrs[name].Member, IsClrOnly.No);
                    }
                }

                foreach (var attr in _attrs.ToArray()) {
                    if (attr.Value.ClrOnly == IsClrOnly.NotChecked) {
                        _attrs[attr.Key] = new MemberInfo(attr.Value.Member, IsClrOnly.Yes);
                    }
                }
                _checkedClrAttrs = true;
            }
        }


        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            var ri = RemoteInterpreter;
            return ri != null ? ri.DirHelper(Value, ((IronPythonModuleContext)moduleContext).ShowClr) : Enumerable.Empty<string>();
        }

        #endregion

        #region IMember Members

        public virtual PythonMemberType MemberType {
            get { return PythonMemberType.Namespace; }
        }

        #endregion

        public override string ToString() {
            return String.Format("{0}({1})", GetType().Name, _obj.IsNull ? "None" : _obj.GetType().ToString());
        }
    }
}
