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
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;

namespace Microsoft.PythonTools.Debugger.DebugEngine {
    // An implementation of IDebugProperty2
    // This interface represents a stack frame property, a program document property, or some other property. 
    // The property is usually the result of an expression evaluation. 
    //
    // The sample engine only supports locals and parameters for functions that have symbols loaded.
    class AD7Property : IDebugProperty2 {
        private PythonEvaluationResult _evalResult;
        private readonly AD7StackFrame _frame;
        private readonly bool _writable;

        public AD7Property(AD7StackFrame frame, PythonEvaluationResult obj, bool writable = false) {
            _evalResult = obj;
            _frame = frame;
            _writable = writable;
        }

        // Construct a DEBUG_PROPERTY_INFO representing this local or parameter.
        public DEBUG_PROPERTY_INFO ConstructDebugPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields) {
            DEBUG_PROPERTY_INFO propertyInfo = new DEBUG_PROPERTY_INFO();

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME) != 0) {
                propertyInfo.bstrFullName = _evalResult.Expression;
                propertyInfo.dwFields |= enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME;
            }

            if ((dwFields & enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_NAME) != 0) {
                if (String.IsNullOrEmpty(_evalResult.ChildText)) {
                    propertyInfo.bstrName = _evalResult.Expression;
                } else {
                    propertyInfo.bstrName = _evalResult.ChildText;
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
                } else {
                    propertyInfo.bstrValue = _evalResult.StringRepr;
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
                if (_evalResult.IsExpandable)
                {
                    propertyInfo.dwAttrib |= enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_OBJ_IS_EXPANDABLE;
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
            var children = _evalResult.GetChildren((int)dwTimeout);
            if (children != null) {
                DEBUG_PROPERTY_INFO[] properties = new DEBUG_PROPERTY_INFO[children.Length];
                for (int i = 0; i < children.Length; i++) {
                    properties[i] = new AD7Property(_frame, children[i], true).ConstructDebugPropertyInfo(dwFields);
                }
                ppEnum = new AD7PropertyEnum(properties);
                return VSConstants.S_OK;
            }
            return VSConstants.S_FALSE;
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
            pPropertyInfo[0] = ConstructDebugPropertyInfo(dwFields);
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
            AutoResetEvent completion = new AutoResetEvent(false);
            this._evalResult.Frame.ExecuteText(_evalResult.Expression + " = " + pszValue, obj => completion.Set());

            while (!_frame.StackFrame.Thread.Process.HasExited && !completion.WaitOne(Math.Min((int)dwTimeout, 100))) {
                if (dwTimeout <= 100) {
                    break;
                }
                dwTimeout -= 100;
            }

            if (_frame.StackFrame.Thread.Process.HasExited || dwTimeout < 0) {
                return VSConstants.E_FAIL;
            }

            return VSConstants.S_OK;
        }

        #endregion

    }
}
