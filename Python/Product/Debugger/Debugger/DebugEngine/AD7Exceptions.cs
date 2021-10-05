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
	sealed class AD7DebugExceptionEvent : AD7StoppingEvent, IDebugExceptionEvent150, IDebugExceptionEvent2
	{
		public const string IID = "51A94113-8788-4A54-AE15-08B74FF922D0";
		private readonly AD7Engine _engine;
		private readonly PythonException _exception;

		public AD7DebugExceptionEvent(AD7Engine engine, PythonException exception)
		{
			_engine = engine;
			_exception = exception;
		}

		#region IDebugExceptionEvent150 Members

		int IDebugExceptionEvent150.GetException(EXCEPTION_INFO150[] pExceptionInfo)
		{
			var info = new EXCEPTION_INFO[1];
			int hr = ((IDebugExceptionEvent2)this).GetException(info);
			if (hr != VSConstants.S_OK)
			{
				return hr;
			}

			pExceptionInfo[0].guidType = info[0].guidType;
			pExceptionInfo[0].bstrExceptionName = info[0].bstrExceptionName;
			pExceptionInfo[0].bstrProgramName = info[0].bstrProgramName;
			pExceptionInfo[0].dwCode = info[0].dwCode;
			pExceptionInfo[0].dwState = (uint)info[0].dwState;
			pExceptionInfo[0].pProgram = info[0].pProgram;
			return VSConstants.S_OK;
		}

		int IDebugExceptionEvent150.GetExceptionDetails(out IDebugExceptionDetails ppDetails)
		{
			ppDetails = new AD7DebugExceptionDetails(_exception);
			return VSConstants.S_OK;
		}

		#endregion

		#region IDebugExceptionEvent2 Members

		int IDebugExceptionEvent2.CanPassToDebuggee()
		{
			return VSConstants.S_FALSE;
		}

		int IDebugExceptionEvent2.GetException(EXCEPTION_INFO[] pExceptionInfo)
		{
			if (pExceptionInfo == null || pExceptionInfo.Length == 0)
			{
				return VSConstants.E_POINTER;
			}

			pExceptionInfo[0].guidType = AD7Engine.DebugEngineGuid;
			pExceptionInfo[0].bstrExceptionName = _exception.TypeName;
			pExceptionInfo[0].pProgram = _engine;
			if (_exception.UserUnhandled)
			{
				pExceptionInfo[0].dwState = enum_EXCEPTION_STATE.EXCEPTION_STOP_USER_UNCAUGHT;
			}
			else
			{
				pExceptionInfo[0].dwState = enum_EXCEPTION_STATE.EXCEPTION_STOP_FIRST_CHANCE;
			}
			if (_exception.HResult != 0)
			{
				pExceptionInfo[0].dwState |= enum_EXCEPTION_STATE.EXCEPTION_CODE_SUPPORTED |
					enum_EXCEPTION_STATE.EXCEPTION_CODE_DISPLAY_IN_HEX;
			}

			return VSConstants.S_OK;
		}

		int IDebugExceptionEvent2.GetExceptionDescription(out string pbstrDescription)
		{
			pbstrDescription = _exception.GetDescription(false);
			return VSConstants.S_OK;
		}

		int IDebugExceptionEvent2.PassToDebuggee(int fPass)
		{
			if (fPass != 0)
			{
				return VSConstants.S_OK;
			}
			return VSConstants.E_FAIL;
		}

		#endregion
	}

	sealed class AD7DebugExceptionDetails : IDebugExceptionDetails
	{
		private readonly PythonException _exception;

		public AD7DebugExceptionDetails(PythonException exception)
		{
			if (exception == null)
			{
				throw new ArgumentNullException(nameof(exception));
			}
			_exception = exception;
		}

		int IDebugExceptionDetails.GetExceptionMessage(out string pbstrMessage)
		{
			pbstrMessage = _exception.ExceptionMessage;
			return string.IsNullOrEmpty(_exception.ExceptionMessage) ? VSConstants.S_FALSE : VSConstants.S_OK;
		}

		int IDebugExceptionDetails.GetExceptionObjectExpression(out string pbstrExceptionObjectExpression)
		{
			pbstrExceptionObjectExpression = _exception.ExceptionObjectExpression;
			return string.IsNullOrEmpty(_exception.ExceptionObjectExpression) ? VSConstants.S_FALSE : VSConstants.S_OK;
		}

		int IDebugExceptionDetails.GetFormattedDescription(IDebugStackFrame2 pStackFrameContext, out string pbstrDescription)
		{
			pbstrDescription = _exception.GetDescription(true);
			return VSConstants.S_OK;
		}

		int IDebugExceptionDetails.GetHResult(out uint pHResult)
		{
			pHResult = _exception.HResult;
			return _exception.HResult == 0 ? VSConstants.S_FALSE : VSConstants.S_OK;
		}

		int IDebugExceptionDetails.GetInnerExceptionDetails(out IDebugExceptionDetails ppDetails)
		{
			if (_exception.InnerException != null)
			{
				ppDetails = new AD7DebugExceptionDetails(_exception.InnerException);
				return VSConstants.S_OK;
			}
			else
			{
				ppDetails = null;
				return VSConstants.S_FALSE;
			}
		}

		int IDebugExceptionDetails.GetSource(out string pbstrSource)
		{
			pbstrSource = _exception.Source;
			return string.IsNullOrEmpty(_exception.Source) ? VSConstants.S_FALSE : VSConstants.S_OK;
		}

		int IDebugExceptionDetails.GetStackTrace(out string pbstrMessage)
		{
			pbstrMessage = _exception.StackTrace;
			return string.IsNullOrEmpty(_exception.StackTrace) ? VSConstants.S_FALSE : VSConstants.S_OK;
		}

		int IDebugExceptionDetails.GetTypeName(int fFullName, out string pbstrTypeName)
		{
			pbstrTypeName = _exception.TypeName;
			if (fFullName == 0 && !string.IsNullOrEmpty(pbstrTypeName))
			{
				pbstrTypeName = pbstrTypeName.Substring(pbstrTypeName.LastIndexOf('.') + 1);
			}
			return string.IsNullOrEmpty(_exception.TypeName) ? VSConstants.S_FALSE : VSConstants.S_OK;
		}
	}
}
