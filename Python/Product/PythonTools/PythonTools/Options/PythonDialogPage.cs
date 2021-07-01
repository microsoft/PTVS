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

namespace Microsoft.PythonTools.Options
{
    /// <summary>
    /// Base class used for saving/loading of settings.  The settings are stored in VSRegistryRoot\PythonTools\Options\Category\SettingName
    /// where Category is provided in the constructor and SettingName is provided to each call of the Save*/Load* APIs.
    /// x = 42
    /// 
    /// The primary purpose of this class is so that we can be in control of providing reasonable default values.
    /// </summary>
    [ComVisible(true)]
    public class PythonDialogPage : DialogPage
    {
        internal PythonToolsService PyService
        {
            get
            {
                return ((IServiceProvider)Site).GetPythonToolsService();
            }
        }

        internal IComponentModel ComponentModel
        {
            get
            {
                return ((IServiceProvider)Site).GetComponentModel();
            }
        }
    }
}
