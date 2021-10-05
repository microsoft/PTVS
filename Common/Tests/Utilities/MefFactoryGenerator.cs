// Visual Studio Shared Project
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

namespace TestUtilities
{
	internal static class MefFactoryGenerator
	{
		private static int _typesCount = 0;
		private static readonly ConstructorInfo _objectCtor;
		private static readonly ConstructorInfo _importingConstructorCtor;
		private static readonly ConstructorInfo _importAttributeCtor;
		private static readonly ConstructorInfo _exportAttributeCtor;
		private static readonly AssemblyBuilder _exportsAssembly;
		private static readonly ModuleBuilder _exportsModule;

		static MefFactoryGenerator()
		{
			_objectCtor = typeof(object).GetConstructor(Type.EmptyTypes);
			_exportAttributeCtor = typeof(System.ComponentModel.Composition.ExportAttribute).GetConstructor(Type.EmptyTypes);
			_importAttributeCtor = typeof(System.ComponentModel.Composition.ImportAttribute).GetConstructor(Type.EmptyTypes);
			_importingConstructorCtor = typeof(System.ComponentModel.Composition.ImportingConstructorAttribute).GetConstructor(Type.EmptyTypes);
			_exportsAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName($"MefExports_{Guid.NewGuid()}"), AssemblyBuilderAccess.Run);
			_exportsModule = _exportsAssembly.DefineDynamicModule("MefExportsModule");
		}

		public static Type GetExportType<T>() where T : new() => GetExportType(() => new T());
		public static Type GetExportType<T>(Func<T> factory) => CreateType<T, Func<T>>().SetFactory(factory);
		public static Type GetExportType<T, TResult>(Func<T, TResult> factory) => CreateType<TResult, Func<T, TResult>>().SetFactory(factory);
		public static Type GetExportType<T1, T2, TResult>(Func<T1, T2, TResult> factory) => CreateType<TResult, Func<T1, T2, TResult>>().SetFactory(factory);

		private static Type SetFactory<TFactory>(this Type type, TFactory factory)
		{
			type.GetField("Factory").SetValue(null, factory);
			return type;
		}

		private static Type CreateType<T, TFactory>()
		{
			var typeBuilder = _exportsModule.DefineType($"<>{typeof(T).Name}_{_typesCount++}",
				TypeAttributes.NotPublic | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoClass | TypeAttributes.AnsiClass,
				typeof(object));
			var factoryField = typeBuilder.DefineField("Factory", typeof(TFactory), FieldAttributes.Static | FieldAttributes.Public);
			var exportField = typeBuilder.DefineField("_export", typeof(T), FieldAttributes.Private);

			typeBuilder.DefineImportingConstructor<TFactory>(factoryField, exportField);
			typeBuilder.DefineExportProperty<T>(exportField);

			return typeBuilder.CreateType();
		}

		private static void DefineImportingConstructor<TFactory>(this TypeBuilder typeBuilder, FieldInfo factoryField, FieldInfo exportField)
		{
			var factoryMethodInfo = typeof(TFactory).GetMethod("Invoke");
			var parameters = factoryMethodInfo.GetParameters();
			var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
			var constructorAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

			var constructor = typeBuilder.DefineConstructor(constructorAttributes, CallingConventions.Standard, parameterTypes);
			var importingConstructorAttribute = new CustomAttributeBuilder(_importingConstructorCtor, new object[0]);
			constructor.SetCustomAttribute(importingConstructorAttribute);


			for (var i = 0; i < parameters.Length; ++i)
			{
				var parameter = parameters[i];
				var parameterBuilder = constructor.DefineParameter(i + 1, parameter.Attributes, parameter.Name);
				var importAttribute = new CustomAttributeBuilder(_importAttributeCtor, new object[0]);
				parameterBuilder.SetCustomAttribute(importAttribute);
			}

			var ilGenerator = constructor.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Call, _objectCtor);

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Ldsfld, factoryField);
			for (int i = 1; i <= parameters.Length; i++)
			{
				ilGenerator.Emit(OpCodes.Ldarg, i);
			}
			ilGenerator.EmitCall(OpCodes.Callvirt, factoryMethodInfo, null);
			ilGenerator.Emit(OpCodes.Stfld, exportField);
			ilGenerator.Emit(OpCodes.Ret);
		}

		private static void DefineExportProperty<T>(this TypeBuilder typeBuilder, FieldInfo exportField)
		{
			var getter = typeBuilder.DefineMethod("get_Export",
				MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
				typeof(T),
				Type.EmptyTypes);

			var ilGenerator = getter.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Ldfld, exportField);
			ilGenerator.Emit(OpCodes.Ret);

			var property = typeBuilder.DefineProperty("Export", PropertyAttributes.None, typeof(T), null);
			var attribute = new CustomAttributeBuilder(_exportAttributeCtor, new object[0]);
			property.SetCustomAttribute(attribute);
			property.SetGetMethod(getter);
		}
	}
}