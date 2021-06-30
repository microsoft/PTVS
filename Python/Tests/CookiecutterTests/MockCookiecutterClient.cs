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
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Model;

namespace CookiecutterTests {
    class MockCookiecutterClient : ICookiecutterClient {
        public bool CookiecutterInstalled {
            get {
                throw new NotImplementedException();
            }
        }

        public Task CreateCookiecutterEnv() {
            throw new NotImplementedException();
        }

        public Task<CreateFilesOperationResult> CreateFilesAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath) {
            throw new NotImplementedException();
        }

        public Task<string> GetDefaultOutputFolderAsync(string shortName) {
            throw new NotImplementedException();
        }

        public Task InstallPackage() {
            throw new NotImplementedException();
        }

        public Task<bool> IsCookiecutterInstalled() {
            throw new NotImplementedException();
        }

        public Task<TemplateContext> LoadUnrenderedContextAsync(string localTemplateFolder, string userConfigFilePath) {
            throw new NotImplementedException();
        }

        public Task<TemplateContext> LoadRenderedContextAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath) {
            throw new NotImplementedException();
        }
    }
}
