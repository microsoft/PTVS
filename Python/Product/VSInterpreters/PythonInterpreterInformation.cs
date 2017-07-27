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
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    class PythonInterpreterInformation {
        IPythonInterpreterFactory Factory;
        public readonly InterpreterConfiguration Configuration;
        public readonly string Vendor;
        public readonly string VendorUrl;
        public readonly string SupportUrl;

        private const string ExperimentSubkey = @"Software\Microsoft\PythonTools\Experimental";
        private const string ExperimentalFactoryKey = "NoDatabaseFactory";
        private static readonly Lazy<bool> _experimentalFactory = new Lazy<bool>(GetExperimentalFactoryFlag);

        private static bool GetExperimentalFactoryFlag() {
            using (var root = Registry.CurrentUser.OpenSubKey(ExperimentSubkey, false)) {
                var value = root?.GetValue(ExperimentalFactoryKey);
                int? asInt = value as int?;
                if (asInt.HasValue) {
                    if (asInt.GetValueOrDefault() == 0) {
                        // REG_DWORD but 0 means no experiment
                        return false;
                    }
                } else if (string.IsNullOrEmpty(value as string)) {
                    // Empty string or no value means no experiment
                    return false;
                }
            }
            return true;
        }

        public PythonInterpreterInformation(
            InterpreterConfiguration configuration,
            string vendor,
            string vendorUrl,
            string supportUrl
        ) {
            Configuration = configuration;
            Vendor = vendor;
            VendorUrl = vendorUrl;
            SupportUrl = supportUrl;
        }

        public IPythonInterpreterFactory EnsureFactory() {
            if (Factory == null) {
                lock (this) {
                    if (Factory == null) {
                        Factory = InterpreterFactoryCreator.CreateInterpreterFactory(
                            Configuration,
                            new InterpreterFactoryCreationOptions {
                                PackageManager = new PipPackageManager(),
                                WatchFileSystem = true,
                                NoDatabase = _experimentalFactory.Value
                            }
                        );
                    }
                }
            }
            return Factory;
        }
    }
}
