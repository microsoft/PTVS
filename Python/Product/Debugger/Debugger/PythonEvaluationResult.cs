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

namespace Microsoft.PythonTools.Debugger
{
	internal enum PythonEvaluationResultReprKind
	{
		Normal,
		Raw,
		RawLen
	}

	[Flags]
	internal enum PythonEvaluationResultFlags
	{
		None = 0,
		Expandable = 1,
		MethodCall = 2,
		SideEffects = 4,
		Raw = 8,
		HasRawRepr = 16,
	}

	/// <summary>
	/// Represents the result of an evaluation of an expression against a given stack frame.
	/// </summary>
	internal class PythonEvaluationResult
	{
		private readonly string _objRepr, _hexRepr, _typeName, _expression, _childName, _exceptionText;
		private readonly PythonStackFrame _frame;
		private readonly PythonProcess _process;
		private readonly PythonEvaluationResultFlags _flags;
		private readonly long _length;

		/// <summary>
		/// Creates a PythonObject for an expression which successfully returned a value.
		/// </summary>
		public PythonEvaluationResult(PythonProcess process, string objRepr, string hexRepr, string typeName, long length, string expression, string childName, PythonStackFrame frame, PythonEvaluationResultFlags flags)
		{
			_process = process;
			_objRepr = objRepr;
			_hexRepr = hexRepr;
			_typeName = typeName;
			_length = length;
			_expression = expression;
			_childName = childName;
			_frame = frame;
			_flags = flags;
		}

		/// <summary>
		/// Creates a PythonObject for an expression which raised an exception instead of returning a value.
		/// </summary>
		public PythonEvaluationResult(PythonProcess process, string exceptionText, string expression, PythonStackFrame frame)
		{
			_process = process;
			_expression = expression;
			_frame = frame;
			_exceptionText = exceptionText;
		}

		public PythonEvaluationResultFlags Flags => _flags;

		/// <summary>
		/// Returns true if this object is expandable.  
		/// </summary>
		public bool IsExpandable => _flags.HasFlag(PythonEvaluationResultFlags.Expandable);

		/// <summary>
		/// Gets the list of children which this object contains.  The children can be either
		/// members (x.fob, x.oar) or they can be indexes (x[0], x[1], etc...).  Calling this
		/// causes the children to be determined by communicating with the debuggee.  These
		/// objects can then later be evaluated.  The names returned here are in the form of
		/// "fob" or "0" so they need additional work to append onto this expression.
		/// 
		/// Returns null if the object is not expandable.
		/// </summary>
		public async Task<PythonEvaluationResult[]> GetChildrenAsync(CancellationToken ct)
		{
			if (!IsExpandable)
			{
				return null;
			}

			return await _process.GetChildrenAsync(Expression, _frame, ct);
		}

		/// <summary>
		/// Gets the string representation of this evaluation, or <c>null</c> if repr was not requested or the evaluation
		/// failed with an exception.
		/// </summary>
		public string StringRepr => _objRepr;

		/// <summary>
		/// Gets the string representation of this evaluation in hexadecimal or null if the hex value was not computable.
		/// </summary>
		public string HexRepr => _hexRepr;

		/// <summary>
		/// Gets the type name of the result of this evaluation or null if an exception was thrown.
		/// </summary>
		public string TypeName => _typeName;

		/// <summary>
		/// Gets the length of the evaluated value as reported by <c>len()</c>, or <c>0</c> if evaluation failed with an exception.
		/// </summary>
		public long Length => _length;

		/// <summary>
		/// Gets the text of the exception which was thrown when evaluating this expression, or null
		/// if no exception was thrown.
		/// </summary>
		public string ExceptionText => _exceptionText;

		/// <summary>
		/// Gets the expression which was evaluated to return this object.
		/// </summary>
		public string Expression => _expression;

		/// <summary>
		/// If this evaluation result represents a child of another expression (e.g. an object attribute or a collection element),
		/// the short name of that child that uniquely identifies it relative to the parent; for example: "attr", "[123]", "len()". 
		/// If this is not a child of another expression, <c>null</c>.
		/// </summary>
		public string ChildName => _childName;

		/// <summary>
		/// Returns the stack frame in which this expression was evaluated.
		/// </summary>
		public PythonStackFrame Frame => _frame;
	}
}
