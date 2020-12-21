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

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;

namespace TestUtilities.Python {
    internal static class PythonToolsOptionsImporter {

        private static readonly MethodInfo SaveStringMethodInfo;
        private static readonly MethodInfo IntToStringMethodInfo;
        private static readonly MethodInfo BoolToStringMethodInfo;
        private static readonly MethodInfo NullableBoolToStringMethodInfo;

        static PythonToolsOptionsImporter() {
            var type = typeof(PythonToolsOptionsImporter);
            IntToStringMethodInfo = type.GetMethod(nameof(IntToString), BindingFlags.Static | BindingFlags.NonPublic);
            BoolToStringMethodInfo = type.GetMethod(nameof(BoolToString), BindingFlags.Static | BindingFlags.NonPublic);
            NullableBoolToStringMethodInfo = type.GetMethod(nameof(NullableBoolToString), BindingFlags.Static | BindingFlags.NonPublic);
            SaveStringMethodInfo = typeof(IPythonToolsOptionsService).GetMethod(nameof(IPythonToolsOptionsService.SaveString));
        }

        private static readonly Lazy<Action<IPythonToolsOptionsService, CodeFormattingOptions>[]> ImportFromCodeFormattingOptionsLazy
            = new Lazy<Action<IPythonToolsOptionsService, CodeFormattingOptions>[]>(CreateCodeFormattingOptionsImporters);

        public static void ImportFrom(this IPythonToolsOptionsService service, CodeFormattingOptions options) {
            foreach (var importer in ImportFromCodeFormattingOptionsLazy.Value) {
                importer(service, options);
            }
        }

        private static Action<IPythonToolsOptionsService, CodeFormattingOptions>[] CreateCodeFormattingOptionsImporters() => typeof(CodeFormattingOptions)
            .GetProperties()
            .Where(HasCategory)
            .Select(CreateCodeFormattingOptionImporter)
            .ToArray();

        private static bool HasCategory(PropertyInfo propertyInfo) => OptionCategory.GetOption(propertyInfo.Name) != null;

        private static Action<IPythonToolsOptionsService, CodeFormattingOptions> CreateCodeFormattingOptionImporter(PropertyInfo propertyInfo) {
            var serviceInstance = Expression.Parameter(typeof(IPythonToolsOptionsService));
            var optionsInstance = Expression.Parameter(typeof(CodeFormattingOptions));

            var getterMethodInfo = propertyInfo.GetGetMethod();
            var callGetter = Expression.Call(optionsInstance, getterMethodInfo);
            var valueParameter = CreateCallValue(callGetter, propertyInfo.PropertyType);

            var nameParameter = Expression.Constant(propertyInfo.Name);
            var categoryParameter = Expression.Constant("Formatting");
            var callSaveString = Expression.Call(serviceInstance, SaveStringMethodInfo, nameParameter, categoryParameter, valueParameter);

            return Expression.Lambda<Action<IPythonToolsOptionsService, CodeFormattingOptions>>(callSaveString, serviceInstance, optionsInstance).Compile();
        }

        private static Expression CreateCallValue(Expression callGetter, Type propertyInfoType) {
            if (propertyInfoType == typeof(bool))
                return Expression.Call(BoolToStringMethodInfo, callGetter);
            if (propertyInfoType == typeof(bool?))
                return Expression.Call(NullableBoolToStringMethodInfo, callGetter);
            if (propertyInfoType == typeof(int))
                return Expression.Call(IntToStringMethodInfo, callGetter);
            return callGetter;
        }

        private static string IntToString(int value) => value.ToString();
        private static string BoolToString(bool value) => value.ToString();
        private static string NullableBoolToString(bool? value) => value.HasValue ? value.Value.ToString() : "-";
    }
}