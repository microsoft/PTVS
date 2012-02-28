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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class PythonObject : IMemberContainer, IMember {
        private readonly ObjectIdentityHandle _obj;
        private readonly IronPythonInterpreter _interpreter;
        private Dictionary<string, MemberInfo> _attrs;
        private bool _checkedClrAttrs;

        public PythonObject(IronPythonInterpreter interpreter, ObjectIdentityHandle obj) {
            _interpreter = interpreter;
            _obj = obj;
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
                var res = Interpreter.Remote.GetMember(Value, name);
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
                foreach (var name in Interpreter.Remote.DirHelper(_obj, false)) {
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
            return Interpreter.Remote.DirHelper(Value, ((IronPythonModuleContext)moduleContext).ShowClr);
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
