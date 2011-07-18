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
using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Runtime;

namespace Microsoft.IronPythonTools.Interpreter {
    class PythonObject<T> : IMemberContainer, IMember {
        private readonly T _obj;
        private readonly IronPythonInterpreter _interpreter;
        private IList<string> _nonClrAttributes;

        public PythonObject(IronPythonInterpreter interpreter, T obj) {
            Debug.Assert(!(obj is IronPythonType));
            _interpreter = interpreter;
            _obj = obj;
        }

        public T Value {
            get {
                return _obj;
            }
        }

        public IronPythonInterpreter Interpreter {
            get {
                return _interpreter;
            }
        }

        #region IMemberContainer Members

        public IMember GetMember(IModuleContext context, string name) {
            var res = GetOne(name, context == null || ((IronPythonModuleContext)context).ShowClr);
            if (res != this) {
                return _interpreter.MakeObject(res);
            }
            return null;
        }

        private object GetOne(string index, bool showClr) {
            if (IsVisible(index, showClr)) {
                PythonType pyType = (_obj as PythonType);
                if (pyType != null) {
                    foreach (var baseType in pyType.mro()) {
                        PythonType curType = (baseType as PythonType);
                        if (curType != null) {
                            IDictionary<object, object> dict = new DictProxy(curType);
                            object bresult;
                            if (dict.TryGetValue(index, out bresult)) {
                                return bresult;
                            }
                        }
                    }
                }

                var tracker = _obj as NamespaceTracker;
                if (tracker != null) {
                    object value = NamespaceTrackerOps.GetCustomMember(_interpreter.CodeContext, tracker, index);
                    if (value != OperationFailed.Value) {
                        return value;
                    } else {
                        return this;
                    }
                }
                object result;
                if (_interpreter.TryGetMember(_obj, index, showClr, out result)) {
                    return result;
                }
            }

            return this; // sentinel indicating failure
        }

        public bool IsVisible(string index, bool showClr) {
            if (showClr || _obj is NamespaceTracker) {
                return true;
            }

            if (_nonClrAttributes == null) {
                _nonClrAttributes = IronPythonInterpreter.DirHelper(_obj, false); ;
            }

            return _nonClrAttributes.IndexOf(index) != -1;
        }


        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return IronPythonInterpreter.DirHelper(_obj, ((IronPythonModuleContext)moduleContext).ShowClr);
        }

        #endregion

        #region IMember Members

        public virtual PythonMemberType MemberType {
            get { return PythonMemberType.Namespace; }
        }

        #endregion

        public override string ToString() {
            return String.Format("{0}({1})", GetType().Name, _obj == null ? "None" : _obj.GetType().ToString());
        }
    }
}
