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
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // An implementation of IDebugProperty2
    // This interface represents a stack frame property, a program document property, or some other property. 
    // The property is usually the result of an expression evaluation. 
    //
    // The sample engine only supports locals and parameters for functions that have symbols loaded.
    class AD7Property : IDebugProperty2, IDebugProperty3 {
        private readonly PythonEvaluationResult _evalResult;
        private readonly AD7StackFrame _frame;
        private readonly bool _writable;

        public AD7Property(AD7StackFrame frame, PythonEvaluationResult obj, bool writable = false) {
            _evalResult = obj;
            _frame = frame;
            _writable = writable;
        }

        // Construct a DEBUG_PROPERTY_INFO representing this local or parameter.
        public DEBUG_PROPERTY_INFO ConstructDebugPropertyInfo(uint radix, enum_DEBUGPROP_INFO_FLAGS dwFields) {
            DEBUG_PROPERTY_INFO propertyInfo = new DEBUG_PROPERTY_INFO();

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME) != 0) {
                propertyInfo.bstrFullName = _evalResult.Expression;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME) != 0) {
                if (String.IsNullOrEmpty(_evalResult.ChildName)) {
                    propertyInfo.bstrName = _evalResult.Expression;
                } else {
                    propertyInfo.bstrName = _evalResult.ChildName;
                }
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE) != 0) {
                if (_evalResult.ExceptionText != null) {
                    propertyInfo.bstrType = "<error>";
                } else {
                    propertyInfo.bstrType = _evalResult.TypeName;
                }
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE) != 0) {
                if (_evalResult.ExceptionText != null) {
                    propertyInfo.bstrValue = "error: " + _evalResult.ExceptionText;
                } else if (radix != 16) {
                    propertyInfo.bstrValue = _evalResult.StringRepr;
                } else {
                    propertyInfo.bstrValue = _evalResult.HexRepr ?? _evalResult.StringRepr;
                }
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_ATTRIB) != 0) {
                if (!_writable) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_READONLY;
                }
                if (_evalResult.ExceptionText != null) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_ERROR;
                }
                if (_evalResult.IsExpandable) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE;
                }
                if (_evalResult.Flags.HasFlag(PythonEvaluationResultFlags.MethodCall)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_METHOD;
                }
                if (_evalResult.Flags.HasFlag(PythonEvaluationResultFlags.SideEffects)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_SIDE_EFFECT;
                }
                if (_evalResult.Flags.HasFlag(PythonEvaluationResultFlags.HasRawRepr)) {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_VALUE_RAW_STRING;
                }
            }

            // Always Provide the property so that we can access locals from the automation object.
            propertyInfo.pProperty = (IDebugProperty2)this;
            propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;

            return propertyInfo;
        }

        #region IDebugProperty2 Members

        // Enumerates the children of a property. This provides support for dereferencing pointers, displaying members of an array, or fields of a class or struct.
        // The sample debugger only supports pointer dereferencing as children. This means there is only ever one child.
        public int EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref System.Guid guidFilter, enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter, uint dwTimeout, out IEnumDebugPropertyInfo2 ppEnum) {
            ppEnum = null;
            try {
                var children = TaskHelpers.RunSynchronouslyOnUIThread(ct => {
                    var timeoutToken = CancellationTokens.GetToken(TimeSpan.FromMilliseconds(dwTimeout));
                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutToken);
                    return _evalResult.GetChildrenAsync(linkedSource.Token);
                });

                if (children != null) {
                    DEBUG_PROPERTY_INFO[] properties = new DEBUG_PROPERTY_INFO[children.Length];
                    for (int i = 0; i < children.Length; i++) {
                        properties[i] = new AD7Property(_frame, children[i], true).ConstructDebugPropertyInfo(dwRadix, dwFields);
                    }
                    ppEnum = new AD7PropertyEnum(properties);
                    return VSConstants.S_OK;
                }
                return VSConstants.S_FALSE;
            } catch (OperationCanceledException) {
                return VSConstants.S_FALSE;
            }
        }

        // Returns the property that describes the most-derived property of a property
        // This is called to support object oriented languages. It allows the debug engine to return an IDebugProperty2 for the most-derived 
        // object in a hierarchy. This engine does not support this.
        public int GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost) {
            throw new Exception("The method or operation is not implemented.");
        }

        // This method exists for the purpose of retrieving information that does not lend itself to being retrieved by calling the IDebugProperty2::GetPropertyInfo 
        // method. This includes information about custom viewers, managed type slots and other information.
        // The sample engine does not support this.
        public int GetExtendedInfo(ref System.Guid guidExtendedInfo, out object pExtendedInfo) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the memory bytes for a property value.
        public int GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the memory context for a property value.
        public int GetMemoryContext(out IDebugMemoryContext2 ppMemory) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the parent of a property.
        // The sample engine does not support obtaining the parent of properties.
        public int GetParent(out IDebugProperty2 ppParent) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Fills in a DEBUG_PROPERTY_INFO structure that describes a property.
        public int GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout, IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo) {
            pPropertyInfo[0] = new DEBUG_PROPERTY_INFO();
            rgpArgs = null;
            pPropertyInfo[0] = ConstructDebugPropertyInfo(dwRadix, dwFields);
            return VSConstants.S_OK;
        }

        //  Return an IDebugReference2 for this property. An IDebugReference2 can be thought of as a type and an address.
        public int GetReference(out IDebugReference2 ppReference) {
            throw new Exception("The method or operation is not implemented.");
        }

        // Returns the size, in bytes, of the property value.
        public int GetSize(out uint pdwSize) {
            throw new Exception("The method or operation is not implemented.");
        }

        // The debugger will call this when the user tries to edit the property's values
        // We only accept setting values as strings
        public int SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount, IDebugReference2 pValue, uint dwTimeout) {
            throw new Exception("The method or operation is not implemented.");
        }

        // The debugger will call this when the user tries to edit the property's values in one of the debugger windows.
        public int SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout) {
            try {
                var result = TaskHelpers.RunSynchronouslyOnUIThread(async ct => {
                    var timeoutToken = CancellationTokens.GetToken(TimeSpan.FromMilliseconds(dwTimeout));
                    var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutToken);
                    return await _evalResult.Frame.ExecuteTextAsync(_evalResult.Expression + " = " + pszValue, ct: linkedSource.Token);
                });
                return VSConstants.S_OK;
            } catch (OperationCanceledException) {
                return VSConstants.E_FAIL;
            }
        }

        #endregion

        #region IDebugProperty3 Members

        public int CreateObjectID() {
            return VSConstants.E_NOTIMPL;
        }

        public int DestroyObjectID() {
            return VSConstants.E_NOTIMPL;
        }

        public int GetCustomViewerCount(out uint pcelt) {
            pcelt = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int GetCustomViewerList(uint celtSkip, uint celtRequested, DEBUG_CUSTOM_VIEWER[] rgViewers, out uint pceltFetched) {
            pceltFetched = 0;
            return VSConstants.E_NOTIMPL;
        }

        public int GetStringCharLength(out uint pLen) {
            var result = TaskHelpers.RunSynchronouslyOnUIThread(ct => _evalResult.Frame.ExecuteTextAsync(_evalResult.Expression, PythonEvaluationResultReprKind.RawLen, ct));
            pLen = (uint)(result.ExceptionText != null ? result.ExceptionText.Length : result.Length);
            return VSConstants.S_OK;
        }

        public unsafe int GetStringChars(uint buflen, ushort[] rgString, out uint pceltFetched) {
            if (rgString.Length == 0) {
                pceltFetched = 0;
                return VSConstants.S_OK;
            }

            var result = TaskHelpers.RunSynchronouslyOnUIThread(ct => _evalResult.Frame.ExecuteTextAsync(_evalResult.Expression, PythonEvaluationResultReprKind.Raw, ct));
            var value = result.ExceptionText ?? result.StringRepr;

            pceltFetched = Math.Min(buflen, (uint)value.Length);
            fixed (char* src = value) {
                fixed (ushort* dst = rgString) {
                    Encoding.Unicode.GetBytes(src, checked((int)pceltFetched), (byte*)dst, checked((int)buflen * 2));
                }
            }

            return VSConstants.S_OK;
        }

        public int SetValueAsStringWithError(string pszValue, uint dwRadix, uint dwTimeout, out string errorString) {
            errorString = null;
            return VSConstants.E_NOTIMPL;
        }

        #endregion
    }
}
