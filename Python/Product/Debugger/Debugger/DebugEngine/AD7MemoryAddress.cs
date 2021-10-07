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
	// An implementation of IDebugCodeContext2. 
	// Represents the starting position of a code instruction. 
	// For Python, this is fundamentally a specific line in the source code.
	internal class AD7MemoryAddress : IDebugCodeContext2, IDebugCodeContext100
	{
		private readonly AD7Engine _engine;
		private readonly uint _lineNo;
		private readonly string _filename;
		private readonly PythonStackFrame _frame;
		private IDebugDocumentContext2 _documentContext;

		public AD7MemoryAddress(AD7Engine engine, string filename, uint lineno, PythonStackFrame frame = null)
		{
			_engine = engine;
			_lineNo = (uint)lineno;
			_filename = filename;
			_frame = frame;

			var span = _engine.Process.GetStatementSpan(_filename, (int)_lineNo + 1, 0);
			var startPos = new TEXT_POSITION { dwLine = (uint)(span.Start.Line - 1), dwColumn = (uint)(span.Start.Column - 1) };
			var endPos = new TEXT_POSITION { dwLine = (uint)(span.End.Line - 1), dwColumn = (uint)(span.End.Column - 1) };

			_documentContext = new AD7DocumentContext(filename, startPos, endPos, this, frame != null ? frame.Kind : FrameKind.None);
		}

		public void SetDocumentContext(IDebugDocumentContext2 docContext)
		{
			_documentContext = docContext;
		}

		#region IDebugMemoryContext2 Members

		// Adds a specified value to the current context's address to create a new context.
		public int Add(ulong dwCount, out IDebugMemoryContext2 newAddress)
		{
			newAddress = new AD7MemoryAddress(_engine, _filename, (uint)dwCount + _lineNo);
			return VSConstants.S_OK;
		}

		// Compares the memory context to each context in the given array in the manner indicated by compare flags, 
		// returning an index of the first context that matches.
		public int Compare(enum_CONTEXT_COMPARE uContextCompare, IDebugMemoryContext2[] compareToItems, uint compareToLength, out uint foundIndex)
		{
			foundIndex = uint.MaxValue;

			enum_CONTEXT_COMPARE contextCompare = (enum_CONTEXT_COMPARE)uContextCompare;

			for (uint c = 0; c < compareToLength; c++)
			{
				AD7MemoryAddress compareTo = compareToItems[c] as AD7MemoryAddress;
				if (compareTo == null)
				{
					continue;
				}

				if (!AD7Engine.ReferenceEquals(_engine, compareTo._engine))
				{
					continue;
				}

				bool result;

				switch (contextCompare)
				{
					case enum_CONTEXT_COMPARE.CONTEXT_EQUAL:
						result = (_lineNo == compareTo._lineNo);
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN:
						result = (_lineNo < compareTo._lineNo);
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN:
						result = (_lineNo > compareTo._lineNo);
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_LESS_THAN_OR_EQUAL:
						result = (_lineNo <= compareTo._lineNo);
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_GREATER_THAN_OR_EQUAL:
						result = (_lineNo >= compareTo._lineNo);
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_SAME_SCOPE:
					case enum_CONTEXT_COMPARE.CONTEXT_SAME_FUNCTION:
						if (_frame != null)
						{
							result = compareTo._filename == _filename && (compareTo._lineNo + 1) >= _frame.StartLine && (compareTo._lineNo + 1) <= _frame.EndLine;
						}
						else if (compareTo._frame != null)
						{
							result = compareTo._filename == _filename && (_lineNo + 1) >= compareTo._frame.StartLine && (compareTo._lineNo + 1) <= compareTo._frame.EndLine;
						}
						else
						{
							result = _lineNo == compareTo._lineNo && _filename == compareTo._filename;
						}
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_SAME_MODULE:
						result = _filename == compareTo._filename;
						break;

					case enum_CONTEXT_COMPARE.CONTEXT_SAME_PROCESS:
						result = true;
						break;

					default:
						// A new comparison was invented that we don't support
						return VSConstants.E_NOTIMPL;
				}

				if (result)
				{
					foundIndex = c;
					return VSConstants.S_OK;
				}
			}

			return VSConstants.S_FALSE;
		}

		public uint LineNumber => _lineNo;

		// Gets information that describes this context.
		public int GetInfo(enum_CONTEXT_INFO_FIELDS dwFields, CONTEXT_INFO[] pinfo)
		{
			pinfo[0].dwFields = 0;

			if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS) != 0)
			{
				pinfo[0].bstrAddress = _lineNo.ToString();
				pinfo[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_ADDRESS;
			}

			if ((dwFields & enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION) != 0)
			{
				pinfo[0].bstrFunction = _frame != null ? _frame.FunctionName : Strings.DebugUnknownFunctionName;
				pinfo[0].dwFields |= enum_CONTEXT_INFO_FIELDS.CIF_FUNCTION;
			}

			return VSConstants.S_OK;
		}

		// Gets the user-displayable name for this context. Not supported for Python.
		public int GetName(out string pbstrName)
		{
			pbstrName = null;
			return VSConstants.E_NOTIMPL;
		}

		// Subtracts a specified value from the current context's address to create a new context.
		public int Subtract(ulong dwCount, out IDebugMemoryContext2 ppMemCxt)
		{
			ppMemCxt = new AD7MemoryAddress(_engine, _filename, (uint)dwCount - _lineNo);
			return VSConstants.S_OK;
		}

		#endregion

		#region IDebugCodeContext2 Members

		// Gets the document context for this code-context
		public int GetDocumentContext(out IDebugDocumentContext2 ppSrcCxt)
		{
			ppSrcCxt = _documentContext;
			return VSConstants.S_OK;
		}

		// Gets the language information for this code context.
		public int GetLanguageInfo(ref string pbstrLanguage, ref Guid pguidLanguage)
		{
			if (_documentContext != null)
			{
				_documentContext.GetLanguageInfo(ref pbstrLanguage, ref pguidLanguage);
				return VSConstants.S_OK;
			}
			else
			{
				return VSConstants.S_FALSE;
			}
		}

		#endregion

		#region IDebugCodeContext100 Members

		// Returns the program being debugged. For Python debug engine, AD7Engine
		// implements IDebugProgram2 which represents the program being debugged.
		int IDebugCodeContext100.GetProgram(out IDebugProgram2 pProgram)
		{
			pProgram = _engine;
			return VSConstants.S_OK;
		}

		#endregion
	}
}
