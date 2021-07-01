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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    public interface IPythonLauncherOptions
    {
        /// <summary>
        /// Saves the current launcher options to storage.
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Loads the current launcher options from storage.
        /// </summary>
        void LoadSettings();

        /// <summary>
        /// Called when a setting has changed which the launcher may want to update to.
        /// </summary>
        void ReloadSetting(string settingName);

        /// <summary>
        /// Provides a notification that the launcher options have been altered but not saved or
        /// are now committed to disk.
        /// </summary>
        event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        /// <summary>
        /// Gets a win forms control which allow editing of the options.
        /// </summary>
        Control Control
        {
            get;
        }
    }
}
