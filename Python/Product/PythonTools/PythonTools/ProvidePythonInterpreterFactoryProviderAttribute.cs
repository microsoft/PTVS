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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Registers a PythonInterpreterFactoryProvider. Only registered providers
    /// will be loaded at runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class ProvidePythonInterpreterFactoryProviderAttribute : RegistrationAttribute {
        private readonly string _id;
        private readonly Type _provider;

        /// <summary>
        /// Registers a PythonInterpreterFactoryProvider.
        /// </summary>
        /// <param name="id">The ID of the provider.</param>
        /// <param name="factoryProvider">
        /// A type in the assembly containing the provider. This does not need
        /// to be the provider object itself. The assembly needs to be deployed
        /// in the root of the package.
        /// </param>
        public ProvidePythonInterpreterFactoryProviderAttribute(string id, Type factoryProvider) {
            _id = id;
            _provider = factoryProvider;
        }

        public override void Register(RegistrationContext context) {
            // HKLM\Software\VisualStudio\<VSVersion>\PythonTools:
            //      InterpreterFactories\
            //          InterpreterID\
            //              CodeBase
            //
            using (var engineKey = context.CreateKey(PythonCoreConstants.BaseRegistryKey + "\\InterpreterFactories")) {
                using (var subKey = engineKey.CreateSubkey(_id)) {
                    var filename = PathUtils.GetFileOrDirectoryName(_provider.Assembly.CodeBase);
                    subKey.SetValue("CodeBase", "$PackageFolder$\\" + filename);
                }
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
