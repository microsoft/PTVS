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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities
{
    public static class PathUtility
    {
        public static string GetServicePath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            // Get the service path
            var servicePath = FindServiceRootDirectory(path);
            
            // Was the service path found?
            if (servicePath == null) throw new InvalidOperationException(Resources.CannotFindServiceRoot);
            
            return servicePath;
        }

        public static string FindServiceRootDirectory(string path)
        {
            //is the settings.json file present in the folder
            var found = Directory.GetFiles(path, Resources.SettingsFileName).Length == 1;
            if (found) 
                return path; //return it
            else
            {
                //find the last slash
                var slash = path.LastIndexOf('\\');
                if (slash > 0)
                {
                    //slash found trim off the last path
                    path = path.Substring(0, slash);
                    //recurse
                    return FindServiceRootDirectory(path);
                }
                //couldn't locate the service root, exit
                return null;
            }
        }

        public static string Combine(params string[] paths)
        {
            string combinedPath = string.Empty;

            foreach (string path in paths)
            {
                combinedPath = Path.Combine(combinedPath, path);
            }

            return combinedPath;
        }
    }
}