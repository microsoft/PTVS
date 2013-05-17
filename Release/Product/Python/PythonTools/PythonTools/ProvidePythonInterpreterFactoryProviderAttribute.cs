/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using Microsoft.VisualStudio.Shell;

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
        /// to be the provider object itself.
        /// </param>
        public ProvidePythonInterpreterFactoryProviderAttribute(Guid id, Type factoryProvider) {
            _id = id.ToString("B");
            _provider = factoryProvider;
        }

        /// <summary>
        /// Registers a PythonInterpreterFactoryProvider.
        /// </summary>
        /// <param name="id">The ID of the provider.</param>
        /// <param name="factoryProvider">
        /// A type in the assembly containing the provider. This does not need
        /// to be the provider object itself.
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
                    var codeBase = new Uri(_provider.Assembly.CodeBase).LocalPath;
                    subKey.SetValue("CodeBase", context.EscapePath(codeBase));
                }
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
