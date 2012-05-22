// ----------------------------------------------------------------------------------
//
// Copyright 2011 Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using System.IO;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    internal class GlobalPathInfo
    {
        public string PublishSettings { get; private set; }
        public string GlobalSettings { get; private set; }
        public static readonly string AzureSdkAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Resources.AzureSdkDirectoryName);

        /// <summary>
        /// Path to the global settings directory used by GlobalComponents.
        /// </summary>
        private static string _globalSettingsDirectory = null;

        /// <summary>
        /// Gets a path to the global settings directory, which defaults to
        /// AzureSdkAppDir.  This can be set internally for the purpose of
        /// testing.
        /// </summary>
        public static string GlobalSettingsDirectory
        {
            get { return _globalSettingsDirectory ?? AzureSdkAppDir; }
            internal set { _globalSettingsDirectory = value; }
        }
	
        public string AzureSdkDirectory { get; private set; }

        public GlobalPathInfo(string rootPath)
        {
            PublishSettings = Path.Combine(rootPath, Resources.PublishSettingsFileName);
            GlobalSettings = Path.Combine(rootPath, Resources.SettingsFileName);
            AzureSdkDirectory = rootPath;
        }
    }
}