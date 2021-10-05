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
	class IronPythonEvent : PythonObject, IPythonEvent
	{
		private IPythonType _eventHandlerType;
		private IList<IPythonType> _parameterTypes;

		public IronPythonEvent(IronPythonInterpreter interpreter, ObjectIdentityHandle eventObj)
			: base(interpreter, eventObj)
		{
		}

		#region IPythonEvent Members

		public override PythonMemberType MemberType
		{
			get { return PythonMemberType.Event; }
		}

		public IPythonType EventHandlerType
		{
			get
			{
				if (_eventHandlerType == null)
				{
					var ri = RemoteInterpreter;
					_eventHandlerType = ri != null ? Interpreter.GetTypeFromType(ri.GetEventPythonType(Value)) : null;
				}
				return _eventHandlerType;
			}
		}

		public IList<IPythonType> GetEventParameterTypes()
		{
			if (_parameterTypes == null)
			{
				var ri = RemoteInterpreter;
				var types = ri != null ? ri.GetEventParameterPythonTypes(Value) : new ObjectIdentityHandle[0];

				var paramTypes = new IPythonType[types.Length];
				for (int i = 0; i < paramTypes.Length; i++)
				{
					paramTypes[i] = Interpreter.GetTypeFromType(types[i]);
				}

				_parameterTypes = paramTypes;
			}
			return _parameterTypes;
		}

		public string Documentation
		{
			get
			{
				var ri = RemoteInterpreter;
				return ri != null ? ri.GetEventDocumentation(Value) : string.Empty;
			}
		}

		#endregion
	}
}
