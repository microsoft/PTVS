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

namespace Microsoft.PythonTools.Debugger.DebugEngine
{
	// Represents a logical stack frame on the thread stack. 
	// Also implements the IDebugExpressionContext interface, which allows expression evaluation and watch windows.
	internal class AD7StackFrame : IDebugStackFrame2, IDebugExpressionContext2, IDebugProperty2
	{
		private readonly AD7Engine _engine;
		private readonly AD7Thread _thread;
		private readonly PythonStackFrame _stackFrame;

		// An array of this frame's parameters
		private readonly PythonEvaluationResult[] _parameters;

		// An array of this frame's locals
		private readonly PythonEvaluationResult[] _locals;

		public AD7StackFrame(AD7Engine engine, AD7Thread thread, PythonStackFrame threadContext)
		{
			_engine = engine;
			_thread = thread;
			_stackFrame = threadContext;

			_parameters = threadContext.Parameters.ToArray();
			_locals = threadContext.Locals.ToArray();
		}

		public PythonStackFrame StackFrame => _stackFrame;

		public AD7Engine Engine => _engine;

		public AD7Thread Thread => _thread;

		#region Non-interface methods

		// Construct a FRAMEINFO for this stack frame with the requested information.
		public void SetFrameInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, out FRAMEINFO frameInfo)
		{
			frameInfo = new FRAMEINFO();

			// The debugger is asking for the formatted name of the function which is displayed in the callstack window.
			// There are several optional parts to this name including the module, argument types and values, and line numbers.
			// The optional information is requested by setting flags in the dwFieldSpec parameter.
			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME) != 0)
			{
				string funcName = _stackFrame.GetQualifiedFunctionName();
				if (funcName == "<module>")
				{
					if (PathUtils.IsValidPath(_stackFrame.FileName))
					{
						funcName = Strings.DebugFileModule.FormatUI(Path.GetFileNameWithoutExtension(_stackFrame.FileName));
					}
					else if (_stackFrame.FileName.EndsWithOrdinal("<string>"))
					{
						funcName = Strings.DebugExecEvalFunctionName;
					}
					else if (_stackFrame.FileName.EndsWithOrdinal("<stdin>"))
					{
						funcName = Strings.DebugReplInputFunctionName;
					}
					else
					{
						funcName = Strings.DebugFileUnknownCode.FormatUI(_stackFrame.FileName);
					}
				}
				else if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_MODULE) != 0)
				{
					funcName = Strings.DebugStackFrameInfoFunctionNameInFileName.FormatUI(
						funcName,
						PathUtils.IsValidPath(_stackFrame.FileName)
							? Path.GetFileNameWithoutExtension(_stackFrame.FileName)
							: _stackFrame.FileName
					);
					frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_MODULE;
				}

				frameInfo.m_bstrFuncName = funcName;
				frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FUNCNAME;

				if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_LINES) != 0)
				{
					frameInfo.m_bstrFuncName = Strings.DebugStackFrameFunctionWithLine.FormatUI(funcName, _stackFrame.LineNo);
					frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FUNCNAME_LINES;
				}
			}

			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_LANGUAGE) != 0)
			{
				switch (_stackFrame.Kind)
				{
					case FrameKind.Python:
						frameInfo.m_bstrLanguage = DebuggerLanguageNames.Python;
						frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_LANGUAGE;
						break;
					case FrameKind.Django:
						frameInfo.m_bstrLanguage = DebuggerLanguageNames.DjangoTemplates;
						frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_LANGUAGE;
						break;
				}
			}

			// The debugger is requesting the name of the module for this stack frame.
			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_MODULE) != 0)
			{
				if (PathUtils.IsValidPath(_stackFrame.FileName))
				{
					frameInfo.m_bstrModule = Path.GetFileNameWithoutExtension(_stackFrame.FileName);
				}
				else if (_stackFrame.FileName.EndsWithOrdinal("<string>"))
				{
					frameInfo.m_bstrModule = Strings.DebugExecEvalModuleName;
				}
				else if (_stackFrame.FileName.EndsWithOrdinal("<stdin>"))
				{
					frameInfo.m_bstrModule = Strings.DebugReplModuleName;
				}
				else
				{
					frameInfo.m_bstrModule = Strings.DebugUnknownModuleName;
				}
				frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_MODULE;
			}

			// The debugger is requesting the IDebugStackFrame2 value for this frame info.
			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_FRAME) != 0)
			{
				frameInfo.m_pFrame = this;
				frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_FRAME;
			}

			// Does this stack frame of symbols loaded?
			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO) != 0)
			{
				frameInfo.m_fHasDebugInfo = 1;
				frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_DEBUGINFO;
			}

			// Is this frame stale?
			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_STALECODE) != 0)
			{
				frameInfo.m_fStaleCode = 0;
				frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_STALECODE;
			}

			// The debugger would like a pointer to the IDebugModule2 that contains this stack frame.
			if ((dwFieldSpec & enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP) != 0)
			{
				// TODO: Module                
				/*
                if (module != null)
                {
                    AD7Module ad7Module = (AD7Module)module.Client;
                    Debug.Assert(ad7Module != null);
                    frameInfo.m_pModule = ad7Module;
                    frameInfo.m_dwValidFields |= enum_FRAMEINFO_FLAGS.FIF_DEBUG_MODULEP;
                }*/
			}
		}

		// Construct an instance of IEnumDebugPropertyInfo2 for the combined locals and parameters.
		private void CreateLocalsPlusArgsProperties(uint radix, out uint elementsReturned, out IEnumDebugPropertyInfo2 enumObject)
		{
			elementsReturned = 0;

			int localsLength = 0;

			if (_locals != null)
			{
				localsLength = _locals.Length;
				elementsReturned += (uint)localsLength;
			}

			if (_parameters != null)
			{
				elementsReturned += (uint)_parameters.Length;
			}
			DEBUG_PROPERTY_INFO[] propInfo = new DEBUG_PROPERTY_INFO[elementsReturned];

			if (_locals != null)
			{
				for (int i = 0; i < _locals.Length; i++)
				{
					AD7Property property = new AD7Property(this, _locals[i], true);
					propInfo[i] = property.ConstructDebugPropertyInfo(radix, enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
				}
			}

			if (_parameters != null)
			{
				for (int i = 0; i < _parameters.Length; i++)
				{
					AD7Property property = new AD7Property(this, _parameters[i], true);
					propInfo[localsLength + i] = property.ConstructDebugPropertyInfo(radix, enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
				}
			}

			enumObject = new AD7PropertyInfoEnum(propInfo);
		}

		// Construct an instance of IEnumDebugPropertyInfo2 for the locals collection only.
		private void CreateLocalProperties(uint radix, out uint elementsReturned, out IEnumDebugPropertyInfo2 enumObject)
		{
			elementsReturned = (uint)_locals.Length;
			DEBUG_PROPERTY_INFO[] propInfo = new DEBUG_PROPERTY_INFO[_locals.Length];

			for (int i = 0; i < propInfo.Length; i++)
			{
				AD7Property property = new AD7Property(this, _locals[i], true);
				propInfo[i] = property.ConstructDebugPropertyInfo(radix, enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
			}

			enumObject = new AD7PropertyInfoEnum(propInfo);
		}

		// Construct an instance of IEnumDebugPropertyInfo2 for the parameters collection only.
		private void CreateParameterProperties(uint radix, out uint elementsReturned, out IEnumDebugPropertyInfo2 enumObject)
		{
			elementsReturned = (uint)_parameters.Length;
			DEBUG_PROPERTY_INFO[] propInfo = new DEBUG_PROPERTY_INFO[_parameters.Length];

			for (int i = 0; i < propInfo.Length; i++)
			{
				AD7Property property = new AD7Property(this, _parameters[i], true);
				propInfo[i] = property.ConstructDebugPropertyInfo(radix, enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD);
			}

			enumObject = new AD7PropertyInfoEnum(propInfo);
		}

		#endregion

		#region IDebugStackFrame2 Members

		// Creates an enumerator for properties associated with the stack frame, such as local variables.
		// The sample engine only supports returning locals and parameters. Other possible values include
		// class fields (this pointer), registers, exceptions...
		int IDebugStackFrame2.EnumProperties(enum_DEBUGPROP_INFO_FLAGS dwFields, uint nRadix, ref Guid guidFilter, uint dwTimeout, out uint elementsReturned, out IEnumDebugPropertyInfo2 enumObject)
		{
			int hr;

			elementsReturned = 0;
			enumObject = null;

			if (guidFilter == DebuggerConstants.guidFilterLocalsPlusArgs ||
					guidFilter == DebuggerConstants.guidFilterAllLocalsPlusArgs ||
					guidFilter == DebuggerConstants.guidFilterAllLocals)
			{
				CreateLocalsPlusArgsProperties(nRadix, out elementsReturned, out enumObject);
				hr = VSConstants.S_OK;
			}
			else if (guidFilter == DebuggerConstants.guidFilterLocals)
			{
				CreateLocalProperties(nRadix, out elementsReturned, out enumObject);
				hr = VSConstants.S_OK;
			}
			else if (guidFilter == DebuggerConstants.guidFilterArgs)
			{
				CreateParameterProperties(nRadix, out elementsReturned, out enumObject);
				hr = VSConstants.S_OK;
			}
			else
			{
				hr = VSConstants.E_NOTIMPL;
			}
			return hr;
		}

		// Gets the code context for this stack frame. The code context represents the current instruction pointer in this stack frame.
		int IDebugStackFrame2.GetCodeContext(out IDebugCodeContext2 memoryAddress)
		{
			memoryAddress = new AD7MemoryAddress(_engine, _stackFrame.FileName, (uint)_stackFrame.LineNo, _stackFrame);
			return VSConstants.S_OK;
		}

		// Gets a description of the properties of a stack frame.
		// Calling the IDebugProperty2::EnumChildren method with appropriate filters can retrieve the local variables, method parameters, registers, and "this" 
		// pointer associated with the stack frame. The debugger calls EnumProperties to obtain these values in the sample.
		int IDebugStackFrame2.GetDebugProperty(out IDebugProperty2 property)
		{
			property = this;
			return VSConstants.S_OK;
		}

		// Gets the document context for this stack frame. The debugger will call this when the current stack frame is changed
		// and will use it to open the correct source document for this stack frame.
		int IDebugStackFrame2.GetDocumentContext(out IDebugDocumentContext2 docContext)
		{
			docContext = null;

			var span = _engine.Process.GetStatementSpan(StackFrame.FileName, _stackFrame.LineNo, 0);
			var begTp = new TEXT_POSITION { dwLine = (uint)(span.Start.Line - 1), dwColumn = (uint)(span.Start.Column - 1) };
			var endTp = new TEXT_POSITION { dwLine = (uint)(span.End.Line - 1), dwColumn = (uint)(span.End.Column - 1) };

			docContext = new AD7DocumentContext(_stackFrame.FileName, begTp, endTp, null, _stackFrame.Kind);
			return VSConstants.S_OK;
		}

		// Gets an evaluation context for expression evaluation within the current context of a stack frame and thread.
		// Generally, an expression evaluation context can be thought of as a scope for performing expression evaluation. 
		// Call the IDebugExpressionContext2::ParseText method to parse an expression and then call the resulting IDebugExpression2::EvaluateSync 
		// or IDebugExpression2::EvaluateAsync methods to evaluate the parsed expression.
		int IDebugStackFrame2.GetExpressionContext(out IDebugExpressionContext2 ppExprCxt)
		{
			ppExprCxt = (IDebugExpressionContext2)this;
			return VSConstants.S_OK;
		}

		// Gets a description of the stack frame.
		int IDebugStackFrame2.GetInfo(enum_FRAMEINFO_FLAGS dwFieldSpec, uint nRadix, FRAMEINFO[] pFrameInfo)
		{
			SetFrameInfo(dwFieldSpec, out pFrameInfo[0]);

			return VSConstants.S_OK;
		}

		// Gets the language associated with this stack frame. 
		// In this sample, all the supported stack frames are C++
		int IDebugStackFrame2.GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage)
		{
			_stackFrame.Kind.GetLanguageInfo(ref pbstrLanguage, ref pguidLanguage);
			return VSConstants.S_OK;
		}

		// Gets the name of the stack frame.
		// The name of a stack frame is typically the name of the method being executed.
		int IDebugStackFrame2.GetName(out string name)
		{
			name = _stackFrame.FunctionName;
			return 0;
		}

		// Gets a machine-dependent representation of the range of physical addresses associated with a stack frame.
		int IDebugStackFrame2.GetPhysicalStackRange(out ulong addrMin, out ulong addrMax)
		{
			addrMin = 0;
			addrMax = 0;

			return VSConstants.S_OK;
		}

		// Gets the thread associated with a stack frame.
		int IDebugStackFrame2.GetThread(out IDebugThread2 thread)
		{
			thread = _thread;
			return VSConstants.S_OK;
		}

		#endregion

		#region IDebugExpressionContext2 Members

		// Retrieves the name of the evaluation context. 
		// The name is the description of this evaluation context. It is typically something that can be parsed by an expression evaluator 
		// that refers to this exact evaluation context. For example, in C++ the name is as follows: 
		// "{ function-name, source-file-name, module-file-name }"
		int IDebugExpressionContext2.GetName(out string pbstrName)
		{
			pbstrName = String.Format("{{ {0} {1} }}", _stackFrame.FunctionName, _stackFrame.FileName);
			return VSConstants.S_OK;
		}

		// Parses a text-based expression for evaluation.
		// The engine sample only supports locals and parameters so the only task here is to check the names in those collections.
		int IDebugExpressionContext2.ParseText(string pszCode,
												enum_PARSEFLAGS dwFlags,
												uint nRadix,
												out IDebugExpression2 ppExpr,
												out string pbstrError,
												out uint pichError)
		{
			pbstrError = "";
			pichError = 0;
			ppExpr = null;

			if (_parameters != null)
			{
				foreach (PythonEvaluationResult currVariable in _parameters)
				{
					if (String.CompareOrdinal(currVariable.Expression, pszCode) == 0)
					{
						ppExpr = new UncalculatedAD7Expression(this, currVariable.Expression, true);
						return VSConstants.S_OK;
					}
				}
			}

			if (_locals != null)
			{
				foreach (PythonEvaluationResult currVariable in _locals)
				{
					if (String.CompareOrdinal(currVariable.Expression, pszCode) == 0)
					{
						ppExpr = new UncalculatedAD7Expression(this, currVariable.Expression, true);
						return VSConstants.S_OK;
					}
				}
			}

			if (!_stackFrame.TryParseText(pszCode, out global::System.String errorMsg))
			{
				pbstrError = Strings.DebugStackFrameParseTextError.FormatUI(errorMsg);
				pichError = (uint)pbstrError.Length;
			}

			ppExpr = new UncalculatedAD7Expression(this, pszCode);
			return VSConstants.S_OK;
		}

		#endregion

		int IDebugProperty2.EnumChildren(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, ref Guid guidFilter, enum_DBG_ATTRIB_FLAGS dwAttribFilter, string pszNameFilter, uint dwTimeout, out IEnumDebugPropertyInfo2 ppEnum)
		{
			int hr;

			uint elementsReturned = 0;
			ppEnum = null;

			if (guidFilter == DebuggerConstants.guidFilterLocalsPlusArgs ||
					guidFilter == DebuggerConstants.guidFilterAllLocalsPlusArgs ||
					guidFilter == DebuggerConstants.guidFilterAllLocals)
			{
				CreateLocalsPlusArgsProperties(dwRadix, out elementsReturned, out ppEnum);
				hr = VSConstants.S_OK;
			}
			else if (guidFilter == DebuggerConstants.guidFilterLocals)
			{
				CreateLocalProperties(dwRadix, out elementsReturned, out ppEnum);
				hr = VSConstants.S_OK;
			}
			else if (guidFilter == DebuggerConstants.guidFilterArgs)
			{
				CreateParameterProperties(dwRadix, out elementsReturned, out ppEnum);
				hr = VSConstants.S_OK;
			}
			else
			{
				hr = VSConstants.E_NOTIMPL;
			}
			return hr;
		}

		int IDebugProperty2.GetDerivedMostProperty(out IDebugProperty2 ppDerivedMost)
		{
			ppDerivedMost = null;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetExtendedInfo(ref Guid guidExtendedInfo, out object pExtendedInfo)
		{
			pExtendedInfo = null;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetMemoryBytes(out IDebugMemoryBytes2 ppMemoryBytes)
		{
			ppMemoryBytes = null;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetMemoryContext(out IDebugMemoryContext2 ppMemory)
		{
			ppMemory = null;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetParent(out IDebugProperty2 ppParent)
		{
			ppParent = null;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS dwFields, uint dwRadix, uint dwTimeout, IDebugReference2[] rgpArgs, uint dwArgCount, DEBUG_PROPERTY_INFO[] pPropertyInfo)
		{
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetReference(out IDebugReference2 ppReference)
		{
			ppReference = null;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.GetSize(out uint pdwSize)
		{
			pdwSize = 0;
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.SetValueAsReference(IDebugReference2[] rgpArgs, uint dwArgCount, IDebugReference2 pValue, uint dwTimeout)
		{
			return VSConstants.E_NOTIMPL;
		}

		int IDebugProperty2.SetValueAsString(string pszValue, uint dwRadix, uint dwTimeout)
		{
			return VSConstants.E_NOTIMPL;
		}
	}
}

