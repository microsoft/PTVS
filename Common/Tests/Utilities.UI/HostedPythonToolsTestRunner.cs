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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using TestRunnerInterop;
using Task = System.Threading.Tasks.Task;

namespace TestUtilities.UI {
    [ComVisible(true)]
    public sealed class HostedPythonToolsTestResult : IVsHostedPythonToolsTestResult {
        public bool IsSuccess { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionTraceback { get; set; }
    }

    [ComVisible(true)]
    public sealed class HostedPythonToolsTestRunner : IVsHostedPythonToolsTest {
        private readonly Assembly _assembly;

        private readonly Dictionary<Type, object> _activeInstances;

        public HostedPythonToolsTestRunner(Assembly assembly) {
            _assembly = assembly;
            _activeInstances = new Dictionary<Type, object>();
        }

        public IVsHostedPythonToolsTestResult Execute(string name, object[] arguments) {
            var parts = name.Split('.');
            var type = _assembly.GetType(string.Join(".", parts.Take(parts.Length - 1)));
            if (type != null) {
                var method = type.GetMethod(parts.Last(), BindingFlags.Instance | BindingFlags.Public);
                if (method != null) {
                    return InvokeTest(type, method, arguments);
                }
            }

            return new HostedPythonToolsTestResult {
                IsSuccess = false,
                ExceptionType = typeof(MissingMethodException).FullName,
                ExceptionMessage = $"Failed to find test \"{name}\" in \"{_assembly.FullName}\"",
                ExceptionTraceback = null
            };
        }

        public void Dispose() {
            foreach (var inst in _activeInstances.Values) {
                (inst as IDisposable)?.Dispose();
            }
            _activeInstances.Clear();
        }

        private IVsHostedPythonToolsTestResult InvokeTest(Type type, MethodInfo method, object[] arguments) {
            object instance;
            if (!_activeInstances.TryGetValue(type, out instance)) {
                instance = Activator.CreateInstance(type);
                _activeInstances[type] = instance;
            }

            var args = new List<object>();
            var inputArgs = arguments.ToList();
            var sp = ServiceProvider.GlobalProvider;
            var dte = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            try {
                foreach (var a in method.GetParameters()) {
                    if (a.ParameterType.IsAssignableFrom(typeof(IServiceProvider))) {
                        args.Add(sp);
                    } else if (a.ParameterType.IsAssignableFrom(typeof(EnvDTE.DTE))) {
                        args.Add(dte);
                    } else if (inputArgs.Count > 0 && a.ParameterType.IsAssignableFrom(inputArgs[0].GetType())) {
                        args.Add(inputArgs[0]);
                        inputArgs.RemoveAt(0);
                    } else {
                        args.Add(ConstructParameter(a.ParameterType, sp, dte, inputArgs));
                    }
                }
            } catch (Exception ex) {
                return new HostedPythonToolsTestResult {
                    IsSuccess = false,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = "Failed to invoke test method with correct arguments." + Environment.NewLine + ex.Message,
                    ExceptionTraceback = ex.StackTrace
                };
            }

            AssertListener.Initialize();

            try {
                try {
                    if (typeof(Task).IsAssignableFrom(method.ReturnType)) {
                        ThreadHelper.JoinableTaskFactory.Run(() => (Task)method.Invoke(instance, args.ToArray()));
                    } else {
                        method.Invoke(instance, args.ToArray());
                    }

                    AssertListener.ThrowUnhandled();
                } finally {
                    foreach (var a in args) {
                        if (a == sp || a == dte) {
                            continue;
                        }
                        (a as IDisposable)?.Dispose();
                    }
                }
            } catch (Exception ex) {
                if (ex is TargetInvocationException || ex is AggregateException) {
                    ex = ex.InnerException;
                }

                return new HostedPythonToolsTestResult {
                    IsSuccess = false,
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    ExceptionTraceback = ex.StackTrace
                };
            }

            return new HostedPythonToolsTestResult {
                IsSuccess = true
            };
        }

        private static object ConstructParameter(Type target, object serviceProvider, object dte, List<object> inputArgs) {
            bool hasDefaultConstructor = false;

            foreach (var constructor in target.GetConstructors()) {
                var args = constructor.GetParameters();
                if (args.Length == 0) {
                    hasDefaultConstructor = true;
                }
                if (args.Length != 1) {
                    continue;
                }
                if (args[0].ParameterType.IsAssignableFrom(typeof(IServiceProvider))) {
                    return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { serviceProvider });
                }
                if (args[0].ParameterType.IsAssignableFrom(typeof(EnvDTE.DTE))) {
                    return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { dte });
                }
                if (inputArgs.Count > 0) {
                    var firstArg = inputArgs[0];
                    if (args[0].ParameterType.IsAssignableFrom(firstArg.GetType())) {
                        inputArgs.RemoveAt(0);
                        return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, new[] { firstArg });
                    }
                }
            }

            if (hasDefaultConstructor) {
                return target.InvokeMember(null, BindingFlags.CreateInstance, null, null, null);
            }

            throw new ArgumentException($"Cannot instantiate {target.FullName}");
        }
    }
}
