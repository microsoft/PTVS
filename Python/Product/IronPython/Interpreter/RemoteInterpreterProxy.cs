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
	class RemoteInterpreterProxy : MarshalByRefObject
	{
		private readonly RemoteInterpreter _remoteInterpreter;

		public RemoteInterpreterProxy()
		{
			_remoteInterpreter = new RemoteInterpreter();
		}

		internal IEnumerable<string> GetBuiltinModuleNames()
		{
			return _remoteInterpreter.GetBuiltinModuleNames();
		}

		internal SetAnalysisDirectoriesResult SetAnalysisDirectories(string[] dirs)
		{
			return _remoteInterpreter.SetAnalysisDirectories(dirs);
		}

		internal ObjectHandle LoadAssemblyByName(string name)
		{
			return _remoteInterpreter.LoadAssemblyByName(name);
		}

		internal ObjectHandle LoadAssemblyByPartialName(string name)
		{
			return _remoteInterpreter.LoadAssemblyByPartialName(name);
		}

		internal ObjectHandle LoadAssemblyFromFile(string name)
		{
			return _remoteInterpreter.LoadAssemblyFromFile(name);
		}

		internal ObjectHandle LoadAssemblyFromFileWithPath(string name)
		{
			return _remoteInterpreter.LoadAssemblyFromFileWithPath(name);
		}

		internal ObjectHandle LoadAssemblyFrom(string path)
		{
			return _remoteInterpreter.LoadAssemblyFrom(path);
		}

		internal ObjectIdentityHandle GetBuiltinType(PythonTools.Interpreter.BuiltinTypeId id)
		{
			return _remoteInterpreter.GetBuiltinType(id);
		}

		internal IEnumerable<string> GetModuleNames()
		{
			return _remoteInterpreter.GetModuleNames();
		}

		internal ObjectIdentityHandle LookupNamespace(string name)
		{
			return _remoteInterpreter.LookupNamespace(name);
		}

		internal ObjectKind GetObjectKind(ObjectIdentityHandle obj)
		{
			return _remoteInterpreter.GetObjectKind(obj);
		}

		internal ObjectIdentityHandle GetBuiltinTypeFromType(Type type)
		{
			return _remoteInterpreter.GetBuiltinTypeFromType(type);
		}

		internal bool UnloadAssemblyReference(string assembly)
		{
			return _remoteInterpreter.UnloadAssemblyReference(assembly);
		}

		internal bool LoadAssemblyReference(string assembly)
		{
			return _remoteInterpreter.LoadAssemblyReference(assembly);
		}

		internal bool LoadAssemblyReferenceByName(string assembly)
		{
			return _remoteInterpreter.LoadAssemblyReferenceByName(assembly);
		}

		internal string GetExtensionPropertyDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetExtensionPropertyDocumentation(value);
		}

		internal ObjectIdentityHandle GetExtensionPropertyType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetExtensionPropertyType(value);
		}

		internal ObjectIdentityHandle ImportBuiltinModule(string modName)
		{
			return _remoteInterpreter.ImportBuiltinModule(modName);
		}

		internal bool AddAssembly(ObjectHandle asm)
		{
			return _remoteInterpreter.AddAssembly(asm);
		}

		public override object InitializeLifetimeService()
		{
			return null;
		}

		internal string GetNamespaceName(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetNamespaceName(value);
		}

		internal bool TypeIs<T>(ObjectIdentityHandle obj)
		{
			return _remoteInterpreter.TypeIs<T>(obj);
		}

		internal ObjectIdentityHandle GetBuiltinMethodDescriptorTemplate(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetBuiltinMethodDescriptorTemplate(value);
		}

		internal ObjectIdentityHandle GetFieldType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetFieldType(value);
		}

		internal string GetBuiltinFunctionName(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetBuiltinFunctionName(value);
		}

		internal ObjectIdentityHandle GetPropertyType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPropertyType(value);
		}

		internal bool TypeGroupHasNewOrInitMethods(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.TypeGroupHasNewOrInitMethods(value);
		}

		internal ObjectIdentityHandle GetEventPythonType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetEventPythonType(value);
		}

		internal string GetModuleName(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetModuleName(value);
		}

		internal ObjectIdentityHandle GetParameterPythonType(ObjectIdentityHandle paramInfo)
		{
			return _remoteInterpreter.GetParameterPythonType(paramInfo);
		}

		internal ObjectIdentityHandle GetObjectPythonType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetObjectPythonType(value);
		}

		internal ObjectIdentityHandle GetConstructorDeclaringPythonType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetConstructorDeclaringPythonType(value);
		}

		internal ObjectIdentityHandle GetMember(ObjectIdentityHandle value, string name)
		{
			return _remoteInterpreter.GetMember(value, name);
		}

		internal bool IsInstanceExtensionMethod(ObjectIdentityHandle overload, ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsInstanceExtensionMethod(overload, value);
		}

		internal ObjectIdentityHandle[] GetParametersNoCodeContext(ObjectIdentityHandle overload)
		{
			return _remoteInterpreter.GetParametersNoCodeContext(overload);
		}

		internal ObjectIdentityHandle GetBuiltinFunctionOverloadReturnType(ObjectIdentityHandle overload)
		{
			return _remoteInterpreter.GetBuiltinFunctionOverloadReturnType(overload);
		}

		internal bool IsEnumValue(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsEnumValue(value);
		}

		internal bool LoadWpf()
		{
			return _remoteInterpreter.LoadWpf();
		}

		internal string GetParameterName(ObjectIdentityHandle parameterInfo)
		{
			return _remoteInterpreter.GetParameterName(parameterInfo);
		}

		internal ParameterKind GetParameterKind(ObjectIdentityHandle parameterInfo)
		{
			return _remoteInterpreter.GetParameterKind(parameterInfo);
		}

		internal string GetParameterDefaultValue(ObjectIdentityHandle parameterInfo)
		{
			return _remoteInterpreter.GetParameterDefaultValue(parameterInfo);
		}

		internal string[] DirHelper(ObjectIdentityHandle value, bool showClr)
		{
			return _remoteInterpreter.DirHelper(value, showClr);
		}

		internal bool IsPropertyStatic(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsPropertyStatic(value);
		}

		internal string GetPropertyDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPropertyDocumentation(value);
		}

		internal bool PythonTypeHasNewOrInitMethods(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.PythonTypeHasNewOrInitMethods(value);
		}

		internal ObjectIdentityHandle[] GetPythonTypeConstructors(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPythonTypeConstructors(value);
		}

		internal PythonMemberType GetPythonTypeMemberType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPythonTypeMemberType(value);
		}

		internal string GetPythonTypeName(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPythonTypeName(value);
		}

		internal string GetPythonTypeDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPythonTypeDocumentation(value);
		}

		internal BuiltinTypeId PythonTypeGetBuiltinTypeId(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.PythonTypeGetBuiltinTypeId(value);
		}

		internal ObjectIdentityHandle[] GetPythonTypeMro(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPythonTypeMro(value);
		}

		internal string GetTypeDeclaringModule(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetTypeDeclaringModule(value);
		}

		internal bool IsPythonTypeArray(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsPythonTypeArray(value);
		}

		internal ObjectIdentityHandle GetPythonTypeElementType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetPythonTypeElementType(value);
		}

		internal bool IsDelegateType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsDelegateType(value);
		}

		internal ObjectIdentityHandle[] GetEventInvokeArgs(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetEventInvokeArgs(value);
		}

		internal string GetModuleDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetModuleDocumentation(value);
		}

		internal bool IsPythonTypeGenericTypeDefinition(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsPythonTypeGenericTypeDefinition(value);
		}

		internal string GetBuiltinFunctionDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetBuiltinFunctionDocumentation(value);
		}

		internal ObjectIdentityHandle[] GetBuiltinFunctionOverloads(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetBuiltinFunctionOverloads(value);
		}

		internal ObjectIdentityHandle[] GetConstructorFunctionTargets(ObjectIdentityHandle function)
		{
			return _remoteInterpreter.GetConstructorFunctionTargets(function);
		}

		internal ObjectIdentityHandle GetConstructorFunctionDeclaringType(ObjectIdentityHandle function)
		{
			return _remoteInterpreter.GetConstructorFunctionDeclaringType(function);
		}

		internal ObjectIdentityHandle GetBuiltinFunctionDeclaringPythonType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetBuiltinFunctionDeclaringPythonType(value);
		}

		internal string GetBuiltinFunctionModule(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetBuiltinFunctionModule(value);
		}

		internal string GetTypeGroupDeclaringModule(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetTypeGroupDeclaringModule(value);
		}

		internal string GetTypeGroupDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetTypeGroupDocumentation(value);
		}

		internal string GetTypeGroupName(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetTypeGroupName(value);
		}

		internal PythonMemberType GetTypeGroupMemberType(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetTypeGroupMemberType(value);
		}

		internal ObjectIdentityHandle[] GetTypeGroupEventInvokeArgs(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetTypeGroupEventInvokeArgs(value);
		}

		internal ObjectIdentityHandle PythonTypeMakeGenericType(ObjectIdentityHandle value, ObjectIdentityHandle[] types)
		{
			return _remoteInterpreter.PythonTypeMakeGenericType(value, types);
		}

		internal ObjectIdentityHandle[] GetTypeGroupConstructors(ObjectIdentityHandle value, out ObjectIdentityHandle declType)
		{
			return _remoteInterpreter.GetTypeGroupConstructors(value, out declType);
		}

		internal ObjectIdentityHandle TypeGroupMakeGenericType(ObjectIdentityHandle value, ObjectIdentityHandle[] types)
		{
			return _remoteInterpreter.TypeGroupMakeGenericType(value, types);
		}

		internal bool? TypeGroupIsGenericTypeDefinition(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.TypeGroupIsGenericTypeDefinition(value);
		}

		internal IEnumerable<string> GetNamespaceChildren(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetNamespaceChildren(value);
		}

		internal ObjectIdentityHandle[] GetEventParameterPythonTypes(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetEventParameterPythonTypes(value);
		}

		internal string GetEventDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetEventDocumentation(value);
		}

		internal bool IsFieldStatic(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.IsFieldStatic(value);
		}

		internal string GetFieldDocumentation(ObjectIdentityHandle value)
		{
			return _remoteInterpreter.GetFieldDocumentation(value);
		}
	}

	class MyAppDomainManager : AppDomainManager
	{
		public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
		{
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
			AppDomain.CurrentDomain.TypeResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
			base.InitializeNewDomain(appDomainInfo);
		}

		Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			Debug.WriteLine("AssemblyResolve");
			return null;
		}


	}
}
