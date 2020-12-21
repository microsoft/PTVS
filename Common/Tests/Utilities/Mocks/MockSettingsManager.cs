// Visual Studio Shared Project
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

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;

namespace TestUtilities.Mocks
{
    [ComVisible(true)]
    public class MockSettingsManager : IVsSettingsManager
    {
        public readonly MockSettingsStore Store = new MockSettingsStore();

        public int GetApplicationDataFolder(uint folder, out string folderPath)
        {
            throw new NotImplementedException();
        }

        public int GetCollectionScopes(string collectionPath, out uint scopes)
        {
            throw new NotImplementedException();
        }

        public int GetCommonExtensionsSearchPaths(uint paths, string[] commonExtensionsPaths, out uint actualPaths)
        {
            throw new NotImplementedException();
        }

        public int GetPropertyScopes(string collectionPath, string propertyName, out uint scopes)
        {
            throw new NotImplementedException();
        }

        public int GetReadOnlySettingsStore(uint scope, out IVsSettingsStore store)
        {
            store = Store;
            return VSConstants.S_OK;
        }

        public int GetWritableSettingsStore(uint scope, out IVsWritableSettingsStore writableStore)
        {
            writableStore = Store;
            return VSConstants.S_OK;
        }
    }
}
