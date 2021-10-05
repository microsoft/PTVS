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
	class IronPythonParameterInfo : IParameterInfo
	{
		private readonly IronPythonInterpreter _interpreter;
		private RemoteInterpreterProxy _remote;
		private readonly ObjectIdentityHandle _parameterInfo;
		private string _name;
		private ParameterKind _paramKind;
		private IPythonType[] _paramType;
		private string _defaultValue;
		private static readonly string _noDefaultValue = "<No Default Value>";  // sentinel value to mark when an object doesn't have a default value

		public IronPythonParameterInfo(IronPythonInterpreter interpreter, ObjectIdentityHandle parameterInfo)
		{
			_interpreter = interpreter;
			_interpreter.UnloadingDomain += Interpreter_UnloadingDomain;
			_remote = _interpreter.Remote;
			_parameterInfo = parameterInfo;
		}

		private void Interpreter_UnloadingDomain(object sender, EventArgs e)
		{
			_remote = null;
			_interpreter.UnloadingDomain -= Interpreter_UnloadingDomain;
		}

		#region IParameterInfo Members

		public IList<IPythonType> ParameterTypes
		{
			get
			{
				if (_paramType == null)
				{
					var ri = _remote;
					if (ri != null)
					{
						_paramType = new[] { _interpreter.GetTypeFromType(ri.GetParameterPythonType(_parameterInfo)) };
					}
				}
				return _paramType;
			}
		}

		// FIXME
		public string Documentation
		{
			get { return ""; }
		}

		public string Name
		{
			get
			{
				if (_name == null)
				{
					var ri = _remote;
					_name = ri != null ? ri.GetParameterName(_parameterInfo) : string.Empty;
				}
				return _name;
			}
		}

		public bool IsParamArray
		{
			get
			{
				if (_paramKind == ParameterKind.Unknown)
				{
					var ri = _remote;
					_paramKind = ri != null ? ri.GetParameterKind(_parameterInfo) : ParameterKind.Unknown;
				}
				return _paramKind == ParameterKind.List;
			}
		}

		public bool IsKeywordDict
		{
			get
			{
				if (_paramKind == ParameterKind.Unknown)
				{
					var ri = _remote;
					_paramKind = ri != null ? ri.GetParameterKind(_parameterInfo) : ParameterKind.Unknown;
				}
				return _paramKind == ParameterKind.Dictionary;
			}
		}

		public string DefaultValue
		{
			get
			{
				if (_defaultValue == null)
				{
					var ri = _remote;
					_defaultValue = (ri != null ? ri.GetParameterDefaultValue(_parameterInfo) : null) ?? _noDefaultValue;
				}

				if (Object.ReferenceEquals(_defaultValue, _noDefaultValue))
				{
					return null;
				}

				return _noDefaultValue;
			}
		}


		#endregion
	}
}
