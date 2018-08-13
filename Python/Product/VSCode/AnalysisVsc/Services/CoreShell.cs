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
using Microsoft.DsTools.Core.Diagnostics;
using Microsoft.DsTools.Core.Services;
using Microsoft.DsTools.Core.Services.Shell;

namespace Microsoft.Python.LanguageServer.Services {
    internal sealed class CoreShell : ICoreShell, IDisposable {
        private static CoreShell _instance;

        public static CoreShell Current => _instance;
        public IServiceManager ServiceManager { get; } = new ServiceManager();
        public IServiceContainer Services => ServiceManager;

        public static IDisposable Create() {
            Check.InvalidOperation(() => _instance == null);
            _instance = new CoreShell();
            return _instance;
        }

        private CoreShell() {
            ServiceManager.AddService(this);
        }

        public void Dispose() {
            ServiceManager?.RemoveService(this);
            ServiceManager?.Dispose();
        }
    }
}
