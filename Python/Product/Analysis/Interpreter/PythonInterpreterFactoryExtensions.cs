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
using System.IO;

namespace Microsoft.PythonTools.Interpreter {
    public static class PythonInterpreterFactoryExtensions {
        /// <summary>
        /// Determines whether two interpreter factories are equivalent.
        /// </summary>
        public static bool IsEqual(this IPythonInterpreterFactory x, IPythonInterpreterFactory y) {
            if (x == null || y == null) {
                return x == null && y == null;
            }
            if (x.GetType() != y.GetType()) {
                return false;
            }

            return x.Configuration.Equals(y.Configuration);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory should appear in the UI.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool IsUIVisible(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.Hidden);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory should appear in the UI.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool IsUIVisible(this InterpreterConfiguration config) {
            return config != null &&
                !config.UIMode.HasFlag(InterpreterUIMode.Hidden);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can ever be the default
        /// interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeDefault(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be automatically selected as
        /// the default interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeAutoDefault(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault) &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeAutoDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be automatically selected as
        /// the default interpreter.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeAutoDefault(this InterpreterConfiguration config) {
            return config != null &&
                !config.UIMode.HasFlag(InterpreterUIMode.CannotBeDefault) &&
                !config.UIMode.HasFlag(InterpreterUIMode.CannotBeAutoDefault);
        }

        /// <summary>
        /// Returns <c>true</c> if the factory can be configured.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeConfigured(this InterpreterConfiguration config) { 
            return config != null &&
                !config.UIMode.HasFlag(InterpreterUIMode.CannotBeConfigured);
        }


        /// <summary>
        /// Returns <c>true</c> if the factory can be configured.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public static bool CanBeConfigured(this IPythonInterpreterFactory factory) {
            return factory != null &&
                factory.Configuration != null &&
                !factory.Configuration.UIMode.HasFlag(InterpreterUIMode.CannotBeConfigured);
        }

        /// <summary>
        /// Returns true if the factory can be run. This checks whether the
        /// configured InterpreterPath value is an actual file.
        /// </summary>
        public static bool IsRunnable(this IPythonInterpreterFactory factory) {
            return factory != null && factory.Configuration.IsRunnable();
        }

        /// <summary>
        /// Returns true if the configuration can be run. This checks whether
        /// the configured InterpreterPath value is an actual file.
        /// </summary>
        public static bool IsRunnable(this InterpreterConfiguration config) {
            return config != null &&
                !InterpreterRegistryConstants.IsNoInterpretersFactory(config.Id) &&
                File.Exists(config.InterpreterPath);
        }

        /// <summary>
        /// Checks whether the factory can be run and throws the appropriate
        /// exception if it cannot.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// factory is null and parameterName is provided.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// factory is null and parameterName is not provided, or the factory
        /// has no configuration.
        /// </exception>
        /// <exception cref="NoInterpretersException">
        /// factory is the sentinel used when no environments are installed.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// factory's InterpreterPath does not exist on disk.
        /// </exception>
        public static void ThrowIfNotRunnable(this IPythonInterpreterFactory factory, string parameterName = null) {
            if (factory == null) {
                if (string.IsNullOrEmpty(parameterName)) {
                    throw new NullReferenceException();
                } else {
                    throw new ArgumentNullException(parameterName);
                }
            }
            factory.Configuration.ThrowIfNotRunnable();
        }

        /// <summary>
        /// Checks whether the configuration can be run and throws the
        /// appropriate exception if it cannot.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// config is null and parameterName is provided.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// config is null and parameterName is not provided.
        /// </exception>
        /// <exception cref="NoInterpretersException">
        /// config is the sentinel used when no environments are installed.
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// config's InterpreterPath does not exist on disk.
        /// </exception>
        public static void ThrowIfNotRunnable(this InterpreterConfiguration config, string parameterName = null) {
            if (config == null) {
                if (string.IsNullOrEmpty(parameterName)) {
                    throw new NullReferenceException();
                } else {
                    throw new ArgumentNullException(parameterName);
                }
            } else if (InterpreterRegistryConstants.IsNoInterpretersFactory(config.Id)) {
                throw new NoInterpretersException();
            } else if (!File.Exists(config.InterpreterPath)) {
                throw new FileNotFoundException(config.InterpreterPath ?? "(null)");
            }
        }
    }
}
