// Copyright (c) Ben A Adams. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// 
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestUtilities.Ben.Demystifier {
    public static class StringBuilderExtensions {
        public static StringBuilder AppendException(this StringBuilder stringBuilder, Exception exception) {
            stringBuilder.Append(exception.Message);

            if (exception.InnerException != null) {
                stringBuilder
                    .Append(" ---> ")
                    .AppendException(exception.InnerException)
                    .AppendLine()
                    .Append("   --- End of inner exception stack trace ---");
            }

            var frames = new StackTrace(exception, true).GetFrames();
            if (frames != null && frames.Length > 0) {
                stringBuilder.AppendLine().AppendFrames(frames);
            }

            return stringBuilder;
        }

        public static StringBuilder AppendFrames(this StringBuilder stringBuilder, StackFrame[] frames) {
            if (frames == null || frames.Length == 0) {
                return stringBuilder;
            }

            for (var i = 0; i < frames.Length; i++) {
                var frame = frames[i];
                var method = frame.GetMethod();

                // Always show last stackFrame
                if (!ShowInStackTrace(method) && i != frames.Length - 1) {
                    continue;
                }

                if (i > 0) {
                    stringBuilder.AppendLine();
                }

                stringBuilder
                    .Append("   at ")
                    .AppendMethod(GetMethodDisplay(method));

                var filePath = frame.GetFileName();
                if (!string.IsNullOrEmpty(filePath) && Uri.TryCreate(filePath, UriKind.Absolute, out var uri)) {
                    try {
                        filePath = uri.IsFile ? Path.GetFullPath(filePath) : uri.ToString();
                        stringBuilder.Append(" in ").Append(filePath);
                    } catch (PathTooLongException) { } catch (SecurityException) { }
                }

                var lineNo = frame.GetFileLineNumber();
                if (lineNo != 0) {
                    stringBuilder.Append(":line ");
                    stringBuilder.Append(lineNo);
                }
            }

            return stringBuilder;
        }

        private static void AppendMethod(this StringBuilder builder, ResolvedMethod method) {
            if (method.IsAsync) {
                builder.Append("async ");
            }

            if (method.ReturnParameter.Type != null) {
                builder
                    .AppendParameter(method.ReturnParameter)
                    .Append(" ");
            }

            var isSubMethodOrLambda = !string.IsNullOrEmpty(method.SubMethod) || method.IsLambda;

            builder
                .AppendMethodName(method.Name, method.DeclaringTypeName, isSubMethodOrLambda)
                .Append(method.GenericArguments)
                .AppendParameters(method.Parameters, method.MethodBase != null);

            if (isSubMethodOrLambda) {
                builder
                    .Append("+")
                    .Append(method.SubMethod)
                    .AppendParameters(method.SubMethodParameters, method.SubMethodBase != null);

                if (method.IsLambda) {
                    builder.Append(" => { }");

                    if (method.Ordinal.HasValue){
                        builder.Append(" [");
                        builder.Append(method.Ordinal);
                        builder.Append("]");
                    }
                }
            }
        }

        public static StringBuilder AppendMethodName(this StringBuilder stringBuilder, string name, string declaringTypeName, bool isSubMethodOrLambda) {
            if (!string.IsNullOrEmpty(declaringTypeName)) {
                if (name == ".ctor") {
                    if (!isSubMethodOrLambda)
                        stringBuilder.Append("new ");

                    stringBuilder.Append(declaringTypeName);
                } else if (name == ".cctor") {
                    stringBuilder
                        .Append("static ")
                        .Append(declaringTypeName);
                } else {
                    stringBuilder
                        .Append(declaringTypeName)
                        .Append(".")
                        .Append(name);
                }
            } else {
                stringBuilder.Append(name);
            }

            return stringBuilder;
        }

        private static void AppendParameters(this StringBuilder stringBuilder, List<ResolvedParameter> parameters, bool condition) {
            stringBuilder.Append("(");
            if (parameters != null) {
                if (condition) {
                    stringBuilder.AppendParameters(parameters);
                } else {
                    stringBuilder.Append("?");
                }
            }
            stringBuilder.Append(")");
        }

        private static void AppendParameters(this StringBuilder stringBuilder, List<ResolvedParameter> parameters) {
            var isFirst = true;
            foreach (var param in parameters) {
                if (isFirst) {
                    isFirst = false;
                } else {
                    stringBuilder.Append(", ");
                }
                stringBuilder.AppendParameter(param);
            }
        }

        private static StringBuilder AppendParameter(this StringBuilder stringBuilder, ResolvedParameter parameter) {
            if (!string.IsNullOrEmpty(parameter.Prefix)) {
                stringBuilder.Append(parameter.Prefix).Append(" ");
            }

            stringBuilder.Append(parameter.Type);
            if (!string.IsNullOrEmpty(parameter.Name)) {
                stringBuilder.Append(" ").Append(parameter.Name);
            }

            return stringBuilder;
        }
        
        private static bool ShowInStackTrace(MethodBase method) {
            try {
                var type = method.DeclaringType;
                if (type == null) {
                    return true;
                }

                if (type == typeof(Task<>) && method.Name == "InnerInvoke") {
                    return false;
                }

                if (type == typeof(Task)) {
                    switch (method.Name) {
                        case "ExecuteWithThreadLocal":
                        case "Execute":
                        case "ExecutionContextCallback":
                        case "ExecuteEntry":
                        case "InnerInvoke":
                            return false;
                    }
                }

                if (type == typeof(ExecutionContext)) {
                    switch (method.Name) {
                        case "RunInternal":
                        case "Run":
                            return false;
                    }
                }

                // Don't show any methods marked with the StackTraceHiddenAttribute
                // https://github.com/dotnet/coreclr/pull/14652
                foreach (var attibute in method.GetCustomAttributesData()) {
                    // internal Attribute, match on name
                    if (attibute.AttributeType.Name == "StackTraceHiddenAttribute") {
                        return false;
                    }
                }

                foreach (var attibute in type.GetCustomAttributesData()) {
                    // internal Attribute, match on name
                    if (attibute.AttributeType.Name == "StackTraceHiddenAttribute") {
                        return false;
                    }
                }

                // Fallbacks for runtime pre-StackTraceHiddenAttribute
                if (type == typeof(ExceptionDispatchInfo) && method.Name == nameof(ExceptionDispatchInfo)) {
                    return false;
                }

                if (type == typeof(TaskAwaiter) ||
                    type == typeof(TaskAwaiter<>) ||
                    type == typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter) ||
                    type == typeof(ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter)) {
                    switch (method.Name) {
                        case "HandleNonSuccessAndDebuggerNotification":
                        case "ThrowForNonSuccess":
                        case "ValidateEnd":
                        case "GetResult":
                            return false;
                    }
                } else if (type.FullName == "System.ThrowHelper") {
                    return false;
                }
            } catch {
                // GetCustomAttributesData can throw
                return true;
            }

            return true;
        }

        private static ResolvedMethod GetMethodDisplay(MethodBase originMethod) {
            var methodDisplayInfo = new ResolvedMethod();

            // Special case: no method available
            if (originMethod == null) {
                return methodDisplayInfo;
            }

            var method = originMethod;
            methodDisplayInfo.SubMethodBase = method;

            // Type name
            var type = method.DeclaringType;
            var subMethodName = method.Name;
            var methodName = method.Name;

            if (type != null && type.IsDefined(typeof(CompilerGeneratedAttribute)) &&
                (typeof(IAsyncStateMachine).IsAssignableFrom(type) || typeof(IEnumerator).IsAssignableFrom(type))) {

                methodDisplayInfo.IsAsync = typeof(IAsyncStateMachine).IsAssignableFrom(type);

                // Convert StateMachine methods to correct overload +MoveNext()
                if (!TryResolveStateMachineMethod(ref method, out type)) {
                    methodDisplayInfo.SubMethodBase = null;
                    subMethodName = null;
                }

                methodName = method.Name;
            }

            // Method name
            methodDisplayInfo.MethodBase = method;
            methodDisplayInfo.Name = methodName;
            if (method.Name.IndexOf("<", StringComparison.Ordinal) >= 0) {
                if (TryResolveGeneratedName(ref method, out type, out methodName, out subMethodName, out var kind, out var ordinal)) {
                    methodName = method.Name;
                    methodDisplayInfo.MethodBase = method;
                    methodDisplayInfo.Name = methodName;
                    methodDisplayInfo.Ordinal = ordinal;
                } else {
                    methodDisplayInfo.MethodBase = null;
                }

                methodDisplayInfo.IsLambda = (kind == GeneratedNameKind.LambdaMethod);

                if (methodDisplayInfo.IsLambda && type != null) {
                    if (methodName == ".cctor") {
                        var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var field in fields) {
                            var value = field.GetValue(field);
                            if (value is Delegate d) {
                                if (ReferenceEquals(d.Method, originMethod) && d.Target.ToString() == originMethod.DeclaringType?.ToString()) {
                                    methodDisplayInfo.Name = field.Name;
                                    methodDisplayInfo.IsLambda = false;
                                    method = originMethod;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (subMethodName != methodName) {
                methodDisplayInfo.SubMethod = subMethodName;
            }

            // ResolveStateMachineMethod may have set declaringType to null
            if (type != null) {
                var declaringTypeName = TypeNameHelper.GetTypeDisplayName(type, fullName: true, includeGenericParameterNames: true);
                methodDisplayInfo.DeclaringTypeName = declaringTypeName;
            }

            if (method is MethodInfo mi) {
                var returnParameter = mi.ReturnParameter;
                if (returnParameter != null) {
                    methodDisplayInfo.ReturnParameter = GetParameter(mi.ReturnParameter);
                } else {
                    methodDisplayInfo.ReturnParameter = new ResolvedParameter(string.Empty
                        , TypeNameHelper.GetTypeDisplayName(mi.ReturnType, fullName: false, includeGenericParameterNames: true)
                        , mi.ReturnType
                        , string.Empty);
                }
            }

            if (method.IsGenericMethod) {
                var genericArguments = method.GetGenericArguments();
                var genericArgumentsString = string.Join(", ", genericArguments
                    .Select(arg => TypeNameHelper.GetTypeDisplayName(arg, fullName: false, includeGenericParameterNames: true)));
                methodDisplayInfo.GenericArguments += "<" + genericArgumentsString + ">";
                methodDisplayInfo.ResolvedGenericArguments = genericArguments;
            }

            // Method parameters
            var parameters = method.GetParameters();
            if (parameters.Length > 0) {
                var resolvedParameters = new List<ResolvedParameter>(parameters.Length);
                for (var i = 0; i < parameters.Length; i++) {
                    resolvedParameters.Add(GetParameter(parameters[i]));
                }
                methodDisplayInfo.Parameters = resolvedParameters;
            }

            if (methodDisplayInfo.SubMethodBase == methodDisplayInfo.MethodBase) {
                methodDisplayInfo.SubMethodBase = null;
            } else if (methodDisplayInfo.SubMethodBase != null) {
                parameters = methodDisplayInfo.SubMethodBase.GetParameters();
                if (parameters.Length > 0) {
                    var parameterList = new List<ResolvedParameter>(parameters.Length);
                    foreach (var parameter in parameters) {
                        var param = GetParameter(parameter);
                        if (param.Name?.StartsWith("<") ?? true) {
                            continue;
                        }

                        parameterList.Add(param);
                    }

                    methodDisplayInfo.SubMethodParameters = parameterList;
                }
            }

            return methodDisplayInfo;
        }

        private static bool TryResolveGeneratedName(ref MethodBase method
            , out Type type
            , out string methodName
            , out string subMethodName
            , out GeneratedNameKind kind
            , out int? ordinal) {

            kind = GeneratedNameKind.None;
            type = method.DeclaringType;
            subMethodName = null;
            ordinal = null;
            methodName = method.Name;

            var generatedName = methodName;

            if (!TryParseGeneratedName(generatedName, out kind, out var openBracketOffset, out var closeBracketOffset)) {
                return false;
            }

            methodName = generatedName.Substring(openBracketOffset + 1, closeBracketOffset - openBracketOffset - 1);

            switch (kind) {
                case GeneratedNameKind.LocalFunction: {
                        var localNameStart = generatedName.IndexOf((char)kind, closeBracketOffset + 1);
                        if (localNameStart < 0) break;
                        localNameStart += 3;

                        if (localNameStart < generatedName.Length) {
                            var localNameEnd = generatedName.IndexOf("|", localNameStart, StringComparison.Ordinal);
                            if (localNameEnd > 0) {
                                subMethodName = generatedName.Substring(localNameStart, localNameEnd - localNameStart);
                            }
                        }
                        break;
                    }
                case GeneratedNameKind.LambdaMethod:
                    subMethodName = "";
                    break;
            }

            var dt = method.DeclaringType;
            if (dt == null) {
                return false;
            }

            var matchHint = GetMatchHint(kind, method);
            var matchName = methodName;

            var candidateMethods = dt.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name == matchName);
            if (TryResolveSourceMethod(candidateMethods, kind, matchHint, ref method, ref type, out ordinal)) return true;

            var candidateConstructors = dt.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name == matchName);
            if (TryResolveSourceMethod(candidateConstructors, kind, matchHint, ref method, ref type, out ordinal)) return true;

            const int MaxResolveDepth = 10;
            for (var i = 0; i < MaxResolveDepth; i++) {
                dt = dt.DeclaringType;
                if (dt == null) {
                    return false;
                }

                candidateMethods = dt.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name == matchName);
                if (TryResolveSourceMethod(candidateMethods, kind, matchHint, ref method, ref type, out ordinal)) return true;

                candidateConstructors = dt.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name == matchName);
                if (TryResolveSourceMethod(candidateConstructors, kind, matchHint, ref method, ref type, out ordinal)) return true;

                if (methodName == ".cctor") {
                    candidateConstructors = dt.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => m.Name == matchName);
                    foreach (var cctor in candidateConstructors)
                    {
                        method = cctor;
                        type = dt;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveSourceMethod(IEnumerable<MethodBase> candidateMethods
            , GeneratedNameKind kind
            , string matchHint
            , ref MethodBase method
            , ref Type type
            , out int? ordinal) {

            ordinal = null;
            foreach (var candidateMethod in candidateMethods) {
                var methodBody = candidateMethod.GetMethodBody();
                if (methodBody != null && kind == GeneratedNameKind.LambdaMethod) {
                    foreach (var v in methodBody.LocalVariables) {
                        if (v.LocalType == type) {
                            GetOrdinal(method, ref ordinal);
                        }
                        method = candidateMethod;
                        type = method.DeclaringType;
                        return true;
                    }
                }

                try {
                    var rawIL = methodBody?.GetILAsByteArray();
                    if (rawIL == null) {
                        continue;
                    }

                    var reader = new ILReader(rawIL);
                    while (reader.Read(candidateMethod)) {
                        if (reader.Operand is MethodBase mb) {
                            if (method == mb || (matchHint != null && method.Name.Contains(matchHint))) {
                                if (kind == GeneratedNameKind.LambdaMethod) {
                                    GetOrdinal(method, ref ordinal);
                                }

                                method = candidateMethod;
                                type = method.DeclaringType;
                                return true;
                            }
                        }
                    }
                } catch {
                    // https://github.com/benaadams/Ben.Demystifier/issues/32
                    // Skip methods where il can't be interpreted
                }
            }

            return false;
        }

        private static void GetOrdinal(MethodBase method, ref int? ordinal) {
            var lamdaStart = method.Name.IndexOf((char)GeneratedNameKind.LambdaMethod + "__", StringComparison.Ordinal) + 3;
            if (lamdaStart > 3) {
                var secondStart = method.Name.IndexOf("_", lamdaStart, StringComparison.Ordinal) + 1;
                if (secondStart > 0) {
                    lamdaStart = secondStart;
                }

                if (!int.TryParse(method.Name.Substring(lamdaStart), out var foundOrdinal)) {
                    ordinal = null;
                    return;
                }

                ordinal = foundOrdinal;

                var methods = method.DeclaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                var startName = method.Name.Substring(0, lamdaStart);
                var count = 0;
                foreach (var m in methods) {
                    if (m.Name.Length > lamdaStart && m.Name.StartsWith(startName)) {
                        count++;

                        if (count > 1) {
                            break;
                        }
                    }
                }

                if (count <= 1) {
                    ordinal = null;
                }
            }
        }

        private static string GetMatchHint(GeneratedNameKind kind, MethodBase method) {
            var methodName = method.Name;

            switch (kind) {
                case GeneratedNameKind.LocalFunction:
                    var start = methodName.IndexOf("|", StringComparison.Ordinal);
                    if (start < 1) return null;
                    var end = methodName.IndexOf("_", start, StringComparison.Ordinal) + 1;
                    if (end <= start) return null;

                    return methodName.Substring(start, end - start);
                default:
                    return null;
            }
        }

        // Parse the generated name. Returns true for names of the form
        // [CS$]<[middle]>c[__[suffix]] where [CS$] is included for certain
        // generated names, where [middle] and [__[suffix]] are optional,
        // and where c is a single character in [1-9a-z]
        // (csharp\LanguageAnalysis\LIB\SpecialName.cpp).
        private static bool TryParseGeneratedName(string name, out GeneratedNameKind kind, out int openBracketOffset, out int closeBracketOffset) {
            openBracketOffset = -1;
            if (name.StartsWith("CS$<", StringComparison.Ordinal)) {
                openBracketOffset = 3;
            } else if (name.StartsWith("<", StringComparison.Ordinal)) {
                openBracketOffset = 0;
            }

            if (openBracketOffset >= 0) {
                closeBracketOffset = IndexOfBalancedParenthesis(name, openBracketOffset, '>');
                if (closeBracketOffset >= 0 && closeBracketOffset + 1 < name.Length) {
                    int c = name[closeBracketOffset + 1];
                    // Note '0' is not special.
                    if ((c >= '1' && c <= '9') || (c >= 'a' && c <= 'z'))  {
                        kind = (GeneratedNameKind)c;
                        return true;
                    }
                }
            }

            kind = GeneratedNameKind.None;
            openBracketOffset = -1;
            closeBracketOffset = -1;
            return false;
        }
        
        private static int IndexOfBalancedParenthesis(string str, int openingOffset, char closing) {
            var opening = str[openingOffset];

            var depth = 1;
            for (var i = openingOffset + 1; i < str.Length; i++) {
                var c = str[i];
                if (c == opening) {
                    depth++;
                } else if (c == closing) {
                    depth--;
                    if (depth == 0) {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string GetPrefix(ParameterInfo parameter, Type parameterType) {
            if (parameter.IsOut) {
                return "out";
            }

            if (parameterType != null && parameterType.IsByRef) {
                var attribs = parameter.GetCustomAttributes(inherit: false);
                if (attribs?.Length > 0) {
                    foreach (var attrib in attribs) {
                        if (attrib is Attribute att && att.GetType().Namespace == "System.Runtime.CompilerServices" && att.GetType().Name == "IsReadOnlyAttribute") {
                            return "in";
                        }
                    }
                }

                return "ref";
            }

            return string.Empty;
        }

        private static ResolvedParameter GetParameter(ParameterInfo parameter) {
            var parameterType = parameter.ParameterType;
            var prefix = GetPrefix(parameter, parameterType);
            var parameterTypeString = "?";

            if (parameterType.IsGenericType) {
                var customAttribs = parameter.GetCustomAttributes(inherit: false);

                // We don't use System.ValueTuple yet

                //if (customAttribs.Length > 0) {
                //    var tupleNames = customAttribs.OfType<TupleElementNamesAttribute>().FirstOrDefault()?.TransformNames;

                //    if (tupleNames?.Count > 0) {
                //        return GetValueTupleParameter(tupleNames, prefix, parameter.Name, parameterType);
                //    }
                //}
            }

            if (parameterType.IsByRef) {
                parameterType = parameterType.GetElementType();
            }

            parameterTypeString = TypeNameHelper.GetTypeDisplayName(parameterType, fullName: false, includeGenericParameterNames: true);

            return new ResolvedParameter(parameter.Name, parameterTypeString, parameterType, prefix);
        }

        private static ResolvedParameter GetValueTupleParameter(List<string> tupleNames, string prefix, string name, Type parameterType) {
            var sb = new StringBuilder();
            sb.Append("(");
            var args = parameterType.GetGenericArguments();
            for (var i = 0; i < args.Length; i++) {
                if (i > 0) {
                    sb.Append(", ");
                }

                sb.Append(TypeNameHelper.GetTypeDisplayName(args[i], fullName: false, includeGenericParameterNames: true));

                if (i >= tupleNames.Count) {
                    continue;
                }

                var argName = tupleNames[i];
                if (argName == null) {
                    continue;
                }

                sb.Append(" ");
                sb.Append(argName);
            }

            sb.Append(")");

            return new ResolvedParameter(name, sb.ToString(), parameterType, prefix);
        }

        private static bool TryResolveStateMachineMethod(ref MethodBase method, out Type declaringType) {
            Debug.Assert(method != null);
            Debug.Assert(method.DeclaringType != null);

            declaringType = method.DeclaringType;

            var parentType = declaringType.DeclaringType;
            if (parentType == null) {
                return false;
            }

            var methods = parentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var candidateMethod in methods) {
                var attributes = candidateMethod.GetCustomAttributes<StateMachineAttribute>();

                foreach (var asma in attributes) {
                    if (asma.StateMachineType == declaringType) {
                        method = candidateMethod;
                        declaringType = candidateMethod.DeclaringType;
                        // Mark the iterator as changed; so it gets the + annotation of the original method
                        // async statemachines resolve directly to their builder methods so aren't marked as changed
                        return asma is IteratorStateMachineAttribute;
                    }
                }
            }

            return false;
        }

        private enum GeneratedNameKind {
            None = 0,

            // Used by EE:
            ThisProxyField = '4',
            HoistedLocalField = '5',
            DisplayClassLocalOrField = '8',
            LambdaMethod = 'b',
            LambdaDisplayClass = 'c',
            StateMachineType = 'd',
            LocalFunction = 'g', // note collision with Deprecated_InitializerLocal, however this one is only used for method names

            // Used by EnC:
            AwaiterField = 'u',
            HoistedSynthesizedLocalField = 's',

            // Currently not parsed:
            StateMachineStateField = '1',
            IteratorCurrentBackingField = '2',
            StateMachineParameterProxyField = '3',
            ReusableHoistedLocalField = '7',
            LambdaCacheField = '9',
            FixedBufferField = 'e',
            AnonymousType = 'f',
            TransparentIdentifier = 'h',
            AnonymousTypeField = 'i',
            AutoPropertyBackingField = 'k',
            IteratorCurrentThreadIdField = 'l',
            IteratorFinallyMethod = 'm',
            BaseMethodWrapper = 'n',
            AsyncBuilderField = 't',
            DynamicCallSiteContainerType = 'o',
            DynamicCallSiteField = 'p'
        }

        private struct ResolvedMethod {
            public MethodBase MethodBase { get; set; }
            public string DeclaringTypeName { get; set; }
            public bool IsAsync { get; set; }
            public bool IsLambda { get; set; }
            public ResolvedParameter ReturnParameter { get; set; }
            public string Name { get; set; }
            public int? Ordinal { get; set; }
            public string GenericArguments { get; set; }
            public Type[] ResolvedGenericArguments { get; set; }
            public MethodBase SubMethodBase { get; set; }
            public string SubMethod { get; set; }
            public List<ResolvedParameter> Parameters { get; set; }
            public List<ResolvedParameter> SubMethodParameters { get; set; }
        }

        private struct ResolvedParameter {
            public string Name { get; }
            public string Type { get; }
            public Type ResolvedType { get; }
            public string Prefix { get; }

            public ResolvedParameter(string name, string type, Type resolvedType, string prefix) {
                Name = name;
                Type = type;
                ResolvedType = resolvedType;
                Prefix = prefix;
            }
        }
    }
}