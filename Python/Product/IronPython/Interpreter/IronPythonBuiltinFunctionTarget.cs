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
	class IronPythonBuiltinFunctionTarget : IPythonFunctionOverload
	{
		private readonly IronPythonInterpreter _interpreter;
		private RemoteInterpreterProxy _remote;
		private readonly ObjectIdentityHandle _overload;
		private readonly IronPythonType _declaringType;
		private IParameterInfo[] _params;
		private List<IPythonType> _returnType;

		public IronPythonBuiltinFunctionTarget(IronPythonInterpreter interpreter, ObjectIdentityHandle overload, IronPythonType declType)
		{
			Debug.Assert(interpreter.Remote.TypeIs<MethodBase>(overload));
			_interpreter = interpreter;
			_interpreter.UnloadingDomain += Interpreter_UnloadingDomain;
			_remote = _interpreter.Remote;
			_overload = overload;
			_declaringType = declType;
		}

		private void Interpreter_UnloadingDomain(object sender, EventArgs e)
		{
			_remote = null;
			_interpreter.UnloadingDomain -= Interpreter_UnloadingDomain;
		}

		#region IBuiltinFunctionTarget Members

		// FIXME
		public string Documentation
		{
			get { return ""; }
		}

		// FIXME
		public string ReturnDocumentation
		{
			get { return ""; }
		}

		public IParameterInfo[] GetParameters()
		{
			if (_params == null)
			{
				var ri = _remote;
				bool isInstanceExtensionMethod = ri != null ? ri.IsInstanceExtensionMethod(_overload, _declaringType.Value) : false;

				var parameters = ri != null ? ri.GetParametersNoCodeContext(_overload) : new ObjectIdentityHandle[0];
				var res = new List<IParameterInfo>(parameters.Length);
				foreach (var param in parameters)
				{
					if (res.Count == 0 && isInstanceExtensionMethod)
					{
						// skip instance parameter
						isInstanceExtensionMethod = false;
						continue;
					}
					else
					{
						res.Add(new IronPythonParameterInfo(_interpreter, param));
					}
				}

				_params = res.ToArray();
			}
			return _params;
		}

		public IReadOnlyList<IPythonType> ReturnType
		{
			get
			{
				if (_returnType == null)
				{
					_returnType = new List<IPythonType>();
					var ri = _remote;
					if (ri != null)
					{
						_returnType.Add(_interpreter.GetTypeFromType(ri.GetBuiltinFunctionOverloadReturnType(_overload)));
					}
				}
				return _returnType;
			}
		}

		#endregion
	}
}
