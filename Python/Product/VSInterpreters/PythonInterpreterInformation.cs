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

namespace Microsoft.PythonTools.Interpreter {
    class PythonInterpreterInformation {
        private IPythonInterpreterFactory _factory;

        public readonly InterpreterConfiguration Configuration;
        public readonly string Vendor;
        public readonly string VendorUrl;
        public readonly string SupportUrl;

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

        public IPythonInterpreterFactory GetOrCreateFactory(Func<PythonInterpreterInformation, IPythonInterpreterFactory> creator) {
            if (_factory == null) {
                lock (this) {
                    if (_factory == null) {
                        _factory = creator(this);
                    }
                }
            }
            return _factory;
        }

        private IPackageManager CreatePackageManager() {
            if (ExperimentalOptions.UseCondaPackageManager && !string.IsNullOrEmpty(CondaUtils.GetCondaExecutablePath(Configuration.PrefixPath))) {
                return new CondaPackageManager();
            } else {
                return new PipPackageManager();
            }
        }
    }
}
