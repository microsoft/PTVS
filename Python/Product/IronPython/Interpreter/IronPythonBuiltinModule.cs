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
	internal class IronPythonBuiltinModule : IronPythonModule, IBuiltinPythonModule
	{

		public IronPythonBuiltinModule(IronPythonInterpreter interpreter, ObjectIdentityHandle mod, string name)
			: base(interpreter, mod, name)
		{
		}

		public IMember GetAnyMember(string name)
		{
			switch (name)
			{
				case "NoneType": return Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
				case "generator": return Interpreter.GetBuiltinType(BuiltinTypeId.Generator);
				case "builtin_function": return Interpreter.GetBuiltinType(BuiltinTypeId.BuiltinFunction);
				case "builtin_method_descriptor": return Interpreter.GetBuiltinType(BuiltinTypeId.BuiltinMethodDescriptor);
				case "dict_keys": return Interpreter.GetBuiltinType(BuiltinTypeId.DictKeys);
				case "dict_values": return Interpreter.GetBuiltinType(BuiltinTypeId.DictValues);
				case "function": return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
				case "ellipsis": return Interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
			}

			return base.GetMember(null, name);
		}
	}
}
