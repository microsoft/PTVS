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

namespace Microsoft.CookiecutterTools.Model
{
    interface ICookiecutterClient
    {
        bool CookiecutterInstalled { get; }
        Task<bool> IsCookiecutterInstalled();
        Task CreateCookiecutterEnv();
        Task InstallPackage();
        Task<TemplateContext> LoadUnrenderedContextAsync(string localTemplateFolder, string userConfigFilePath);
        Task<TemplateContext> LoadRenderedContextAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath);
        Task<CreateFilesOperationResult> CreateFilesAsync(string localTemplateFolder, string userConfigFilePath, string contextFilePath, string outputFolderPath);
        Task<string> GetDefaultOutputFolderAsync(string shortName);
    }
}
