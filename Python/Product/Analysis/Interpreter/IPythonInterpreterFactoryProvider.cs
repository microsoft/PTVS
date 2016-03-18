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
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides a source of Python interpreters.  This enables a single implementation
    /// to dynamically lookup the installed Python versions and provide multiple interpreters.
    /// </summary>
    public interface IPythonInterpreterFactoryProvider {
        /// <summary>
        /// Raised when the result of calling <see cref="GetInterpreterConfigurations"/> may have changed.
        /// </summary>
        /// <remarks>New in 2.0.</remarks>
        event EventHandler InterpreterFactoriesChanged;


        /// <summary>
        /// Returns the interpreter configurations that this provider supports.  
        /// 
        /// The configurations returned should be the same instances for subsequent calls.  If the number 
        /// of available configurations can change at runtime new factories can still be returned but the 
        /// existing instances should not be re-created.
        /// </summary>
        IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations();

        /// <summary>
        /// Gets a specific configured interpreter
        /// </summary>
        IPythonInterpreterFactory GetInterpreterFactory(string id);
    }

    public static class PythonInterpreterExtensions {

        /// <summary>
        /// Gets the interpreter factory for a fully qualified interpreter factory name.
        /// 
        /// A fully qualified interpreter factory name is of the form "providerName;identifier".
        /// providerName resolves to the IPythonInterpreterFactoryProvider.  identifier is
        /// an opaque string which the interpreter factory provider uses to resolve the identity
        /// of the interpreter.
        /// </summary>
        public static IPythonInterpreterFactory GetInterpreterFactory(this IEnumerable<Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> factoryProviders, string id) {
            var interpAndId = id.Split(new[] { '|' }, 2);
            if (interpAndId.Length == 2) {
                var provider = factoryProviders.GetInterpreterFactoryProvider(interpAndId[0]);
                if (provider != null) {
                    return provider.GetInterpreterFactory(id);
                }
            }
            return null;
        }

        public static InterpreterConfiguration GetConfiguration(this IEnumerable<Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> factoryProviders, string id) {
            var interpAndId = id.Split(new[] { '|' }, 2);
            if (interpAndId.Length == 2) {
                var provider = factoryProviders.GetInterpreterFactoryProvider(interpAndId[0]);
                if (provider != null) {
                    return provider.GetInterpreterConfigurations().Where(x => x.Id == id).FirstOrDefault();
                }
            }
            return null;
        }

        public static IPythonInterpreterFactoryProvider GetInterpreterFactoryProvider(this IEnumerable<Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> factoryProviders, string id) {
            return factoryProviders.Where(
                x => x.Metadata.ContainsKey("InterpreterFactoryId") &&
                      x.Metadata["InterpreterFactoryId"] is string &&
                      ((string)x.Metadata["InterpreterFactoryId"]) == id
            ).FirstOrDefault()?.Value;
        }

        public static Dictionary<string, InterpreterConfiguration> GetConfigurations(this ExportProvider self) {
            return self.GetExports<IPythonInterpreterFactoryProvider, Dictionary<string, object>>()
                .GetConfigurations();
        }

        public static Dictionary<string, InterpreterConfiguration> GetConfigurations(this IEnumerable<Lazy<IPythonInterpreterFactoryProvider, Dictionary<string, object>>> factoryProviders) {
            Dictionary<string, InterpreterConfiguration> res = new Dictionary<string, InterpreterConfiguration>();
            foreach (var provider in factoryProviders) {
                foreach (var config in provider.Value.GetInterpreterConfigurations()) {
                    res[config.Id] = config;
                }
            }

            return res;
        }

        public static IPythonInterpreterFactory GetInterpreterFactory(this ExportProvider self, string id) {
            return self.GetExports<IPythonInterpreterFactoryProvider, Dictionary<string, object>>()
                .GetInterpreterFactory(id);
        }

        public static bool IsAvailable(this InterpreterConfiguration configuration) {
            // TODO: Differs from original by not checking for base interpreter
            // configuration
            return File.Exists(configuration.InterpreterPath) &&
                File.Exists(configuration.WindowsInterpreterPath) &&
                Directory.Exists(configuration.LibraryPath);
        }

        public static IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories(this IPythonInterpreterFactoryProvider self) {
            return self.GetInterpreterConfigurations().Select(x => self.GetInterpreterFactory(x.Id));
        }
    }
}