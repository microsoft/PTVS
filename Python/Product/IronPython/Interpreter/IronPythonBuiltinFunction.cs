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

namespace Microsoft.IronPythonTools.Interpreter
{
	internal class IronPythonBuiltinFunction : PythonObject, IPythonFunction
	{
		private IronPythonBuiltinFunctionTarget[] _targets;
		private IPythonType _declaringType;
		private IPythonModule _declaringModule;

		public IronPythonBuiltinFunction(IronPythonInterpreter interpreter, ObjectIdentityHandle function)
			: base(interpreter, function)
		{
		}

		#region IBuiltinFunction Members

		public string Name
		{
			get
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				return ri != null ? ri.GetBuiltinFunctionName(Value) : string.Empty;
			}
		}

		public string Documentation
		{
			get
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				return ri != null ? ri.GetBuiltinFunctionDocumentation(Value) : string.Empty;
			}
		}

		public IReadOnlyList<IPythonFunctionOverload> Overloads
		{
			get
			{
				// skip methods that are virtual base helpers (e.g. methods like
				// object.Equals(Object_#1, object other))

				if (_targets == null)
				{
					RemoteInterpreterProxy ri = RemoteInterpreter;
					ObjectIdentityHandle[] overloads = ri != null ? ri.GetBuiltinFunctionOverloads(Value) : new ObjectIdentityHandle[0];
					IronPythonBuiltinFunctionTarget[] result = new IronPythonBuiltinFunctionTarget[overloads.Length];
					IronPythonType decltype = (IronPythonType)DeclaringType;
					for (int i = 0; i < overloads.Length; i++)
					{
						result[i] = new IronPythonBuiltinFunctionTarget(
							Interpreter,
							overloads[i],
							decltype
						);

					}

					_targets = result;
				}

				return _targets;
			}
		}

		public IPythonType DeclaringType
		{
			get
			{
				if (_declaringType == null)
				{
					RemoteInterpreterProxy ri = RemoteInterpreter;
					_declaringType = ri != null ? Interpreter.GetTypeFromType(ri.GetBuiltinFunctionDeclaringPythonType(Value)) : null;
				}
				return _declaringType;
			}
		}

		public bool IsBuiltin => true;

		public bool IsStatic => true;

		public bool IsClassMethod => false;

		public IPythonModule DeclaringModule
		{
			get
			{
				if (_declaringModule == null)
				{
					RemoteInterpreterProxy ri = RemoteInterpreter;
					_declaringModule = ri != null ? Interpreter.GetModule(ri.GetBuiltinFunctionModule(Value)) : null;
				}
				return _declaringModule;
			}
		}

		#endregion

		#region IMember Members

		public override PythonMemberType MemberType => PythonMemberType.Function;

		#endregion

	}
}
