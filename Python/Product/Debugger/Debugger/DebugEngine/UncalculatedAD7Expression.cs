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
	// This class represents a succesfully parsed expression to the debugger. 
	// It is returned as a result of a successful call to IDebugExpressionContext2.ParseText
	// It allows the debugger to obtain the values of an expression in the debuggee. 
	internal class UncalculatedAD7Expression : IDebugExpression2
	{
		private readonly AD7StackFrame _frame;
		private readonly string _expression;
		private readonly bool _writable;

		public UncalculatedAD7Expression(AD7StackFrame frame, string expression, bool writable = false)
		{
			_frame = frame;
			_expression = expression;
			_writable = writable;
		}

		#region IDebugExpression2 Members

		// This method cancels asynchronous expression evaluation as started by a call to the IDebugExpression2::EvaluateAsync method.
		int IDebugExpression2.Abort()
		{
			throw new NotImplementedException();
		}

		// This method evaluates the expression asynchronously.
		// This method should return immediately after it has started the expression evaluation. 
		// When the expression is successfully evaluated, an IDebugExpressionEvaluationCompleteEvent2 
		// must be sent to the IDebugEventCallback2 event callback
		//
		// This is primarily used for the immediate window which this engine does not currently support.
		int IDebugExpression2.EvaluateAsync(enum_EVALFLAGS dwFlags, IDebugEventCallback2 pExprCallback)
		{
			TaskHelpers.RunSynchronouslyOnUIThread(ct => _frame.StackFrame.ExecuteTextAsync(_expression, (obj) =>
			{
				_frame.Engine.Send(
					new AD7ExpressionEvaluationCompleteEvent(this, new AD7Property(_frame, obj, _writable)),
					AD7ExpressionEvaluationCompleteEvent.IID,
					_frame.Engine,
					_frame.Thread);
			}, ct));
			return VSConstants.S_OK;
		}

		// This method evaluates the expression synchronously.
		int IDebugExpression2.EvaluateSync(enum_EVALFLAGS dwFlags, uint dwTimeout, IDebugEventCallback2 pExprCallback, out IDebugProperty2 ppResult)
		{
			AutoResetEvent completion = new AutoResetEvent(false);
			PythonEvaluationResult result = null;

			TaskHelpers.RunSynchronouslyOnUIThread(ct => _frame.StackFrame.ExecuteTextAsync(_expression, (obj) =>
			{
				result = obj;
				completion.Set();
			}, ct));

			while (!_frame.StackFrame.Thread.Process.HasExited && !completion.WaitOne(Math.Min((int)dwTimeout, 100)))
			{
				if (dwTimeout <= 100)
				{
					break;
				}
				dwTimeout -= 100;
			}

			if (_frame.StackFrame.Thread.Process.HasExited || result == null)
			{
				ppResult = null;
				return VSConstants.E_FAIL;
			}
			else if (result == null)
			{
				ppResult = null;
				return DebuggerConstants.E_EVALUATE_TIMEOUT;
			}
			ppResult = new AD7Property(_frame, result, _writable);

			return VSConstants.S_OK;
		}

		#endregion
	}
}