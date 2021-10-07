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
	internal class IronPythonTypeGroup : PythonObject, IAdvancedPythonType
	{
		private bool? _genericTypeDefinition;
		private PythonMemberType _memberType;
		private IList<IPythonType> _eventInvokeArgs;
		private IReadOnlyList<IPythonType> _mro;
		private IPythonFunction _ctors;

		public IronPythonTypeGroup(IronPythonInterpreter interpreter, ObjectIdentityHandle type)
			: base(interpreter, type)
		{
		}

		#region IPythonType Members

		public IPythonFunction GetConstructors()
		{
			if (_ctors == null)
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				if (ri != null && !ri.TypeGroupHasNewOrInitMethods(Value))
				{
					_ctors = GetClrOverloads();
				}

				if (_ctors == null)
				{
					_ctors = GetMember(null, "__new__") as IPythonFunction;
				}
			}
			return _ctors;
		}

		public bool IsPythonType => false;

		/// <summary>
		/// Returns the overloads for a normal .NET type
		/// </summary>
		private IPythonFunction GetClrOverloads()
		{
			ObjectIdentityHandle declType = default(ObjectIdentityHandle);
			RemoteInterpreterProxy ri = RemoteInterpreter;
			ObjectIdentityHandle[] ctors = ri != null ? ri.GetTypeGroupConstructors(Value, out declType) : null;
			if (ctors != null)
			{
				return new IronPythonConstructorFunction(Interpreter, ctors, Interpreter.GetTypeFromType(declType));
			}
			return null;
		}

		public override PythonMemberType MemberType
		{
			get
			{
				if (_memberType == PythonMemberType.Unknown)
				{
					RemoteInterpreterProxy ri = RemoteInterpreter;
					_memberType = ri != null ? ri.GetTypeGroupMemberType(Value) : PythonMemberType.Unknown;
				}
				return _memberType;
			}
		}

		public string Name
		{
			get
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				return ri != null ? ri.GetTypeGroupName(Value) : string.Empty;
			}
		}

		public string Documentation
		{
			get
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				return ri != null ? ri.GetTypeGroupDocumentation(Value) : string.Empty;
			}
		}

		public BuiltinTypeId TypeId => BuiltinTypeId.Unknown;

		public IPythonModule DeclaringModule
		{
			get
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				return ri != null ? Interpreter.ImportModule(ri.GetTypeGroupDeclaringModule(Value)) : null;
			}
		}

		public IReadOnlyList<IPythonType> Mro
		{
			get
			{
				if (_mro == null)
				{
					_mro = new IPythonType[] { this };
				}
				return _mro;
			}
		}

		public bool IsBuiltin => true;

		public IEnumerable<IPythonType> IndexTypes => null;

		public bool IsArray => false;

		public IPythonType GetElementType()
		{
			return null;
		}

		public IList<IPythonType> GetTypesPropagatedOnCall()
		{
			if (_eventInvokeArgs == null)
			{
				RemoteInterpreterProxy ri = RemoteInterpreter;
				ObjectIdentityHandle[] types = ri != null ? ri.GetTypeGroupEventInvokeArgs(Value) : null;
				if (types == null)
				{
					_eventInvokeArgs = IronPythonType.EmptyTypes;
				}
				else
				{
					IPythonType[] args = new IPythonType[types.Length];
					for (int i = 0; i < types.Length; i++)
					{
						args[i] = Interpreter.GetTypeFromType(types[i]);
					}
					_eventInvokeArgs = args;
				}
			}

			return _eventInvokeArgs == IronPythonType.EmptyTypes ? null : _eventInvokeArgs;
		}

		#endregion

		#region IPythonType Members

		public bool IsGenericTypeDefinition
		{
			get
			{
				if (_genericTypeDefinition == null)
				{
					RemoteInterpreterProxy ri = RemoteInterpreter;
					_genericTypeDefinition = ri != null ? ri.TypeGroupIsGenericTypeDefinition(Value) : false;
				}
				return _genericTypeDefinition.Value;
			}
		}

		public IPythonType MakeGenericType(IPythonType[] indexTypes)
		{
			// TODO: Caching?
			ObjectIdentityHandle[] types = new ObjectIdentityHandle[indexTypes.Length];
			for (int i = 0; i < types.Length; i++)
			{
				types[i] = ((IronPythonType)indexTypes[i]).Value;
			}

			RemoteInterpreterProxy ri = RemoteInterpreter;
			return ri != null ? Interpreter.GetTypeFromType(ri.TypeGroupMakeGenericType(Value, types)) : null;
		}

		#endregion

	}
}
