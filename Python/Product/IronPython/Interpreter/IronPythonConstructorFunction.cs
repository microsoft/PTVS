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
	internal class IronPythonConstructorFunction : IPythonFunction
	{
		private readonly ObjectIdentityHandle[] _infos;
		private readonly IronPythonInterpreter _interpreter;
		private RemoteInterpreterProxy _remote;
		private readonly IPythonType _type;
		private IPythonFunctionOverload[] _overloads;
		private IPythonType _declaringType;

		public IronPythonConstructorFunction(IronPythonInterpreter interpreter, ObjectIdentityHandle[] infos, IPythonType type)
		{
			_interpreter = interpreter;
			_interpreter.UnloadingDomain += Interpreter_UnloadingDomain;
			_remote = _interpreter.Remote;
			_infos = infos;
			_type = type;
		}

		private void Interpreter_UnloadingDomain(object sender, EventArgs e)
		{
			_remote = null;
			_interpreter.UnloadingDomain -= Interpreter_UnloadingDomain;
		}

		#region IBuiltinFunction Members

		public string Name => "__new__";

		// TODO: Documentation
		public string Documentation => "";

		public IReadOnlyList<IPythonFunctionOverload> Overloads
		{
			get
			{
				if (_overloads == null)
				{
					IPythonFunctionOverload[] res = new IPythonFunctionOverload[_infos.Length];
					for (int i = 0; i < _infos.Length; i++)
					{
						res[i] = new IronPythonConstructorFunctionTarget(_interpreter, _infos[i], (IronPythonType)DeclaringType);
					}
					_overloads = res;
				}
				return _overloads;
			}
		}

		public IPythonType DeclaringType
		{
			get
			{
				if (_declaringType == null)
				{
					RemoteInterpreterProxy ri = _remote;
					_declaringType = ri != null ? _interpreter.GetTypeFromType(ri.GetConstructorDeclaringPythonType(_infos[0])) : null;
				}
				return _declaringType;
			}
		}

		public bool IsBuiltin => true;

		public bool IsStatic => true;

		public bool IsClassMethod => false;


		public IPythonModule DeclaringModule => _type.DeclaringModule;

		#endregion

		#region IMember Members

		public PythonMemberType MemberType => PythonMemberType.Function;

		#endregion
	}
}
