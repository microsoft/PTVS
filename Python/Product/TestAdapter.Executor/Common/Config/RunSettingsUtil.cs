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
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.PythonTools.TestAdapter.Config {
    public class RunSettingsUtil {
        public static Dictionary<string, PythonProjectSettings> GetSourceToProjSettings(IRunSettings settings, TestFrameworkType filterType) {
            var doc = Read(settings.SettingsXml);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/TestCases/Project");
            var res = new Dictionary<string, PythonProjectSettings>(StringComparer.OrdinalIgnoreCase);

            foreach (XPathNavigator project in nodes) {

                PythonProjectSettings projSettings = new PythonProjectSettings(
                    project.GetAttribute("name", ""),
                    project.GetAttribute("home", ""),
                    project.GetAttribute("workingDir", ""),
                    project.GetAttribute("interpreter", ""),
                    project.GetAttribute("pathEnv", ""),
                    project.GetAttribute("nativeDebugging", "").IsTrue(),
                    project.GetAttribute("isWorkspace", "").IsTrue(),
                    project.GetAttribute("useLegacyDebugger", "").IsTrue(),
                    project.GetAttribute("testFramework", ""),
                    project.GetAttribute("unitTestPattern", ""),
                    project.GetAttribute("unitTestRootDir", ""),
                    project.GetAttribute("discoveryWaitTime", "")
                );

                if (projSettings.TestFramework != filterType) {
                    continue;
                }

                foreach (XPathNavigator environment in project.Select("Environment/Variable")) {
                    projSettings.Environment[environment.GetAttribute("name", "")] = environment.GetAttribute("value", "");
                }

                string djangoSettings = project.GetAttribute("djangoSettingsModule", "");
                if (!String.IsNullOrWhiteSpace(djangoSettings)) {
                    projSettings.Environment["DJANGO_SETTINGS_MODULE"] = djangoSettings;
                }

                foreach (XPathNavigator searchPath in project.Select("SearchPaths/Search")) {
                    projSettings.SearchPath.Add(searchPath.GetAttribute("value", ""));
                }

                foreach (XPathNavigator test in project.Select("Test")) {
                    string testFile = test.GetAttribute("file", "");
                    projSettings.TestContainerSources.Add(testFile, testFile);
                    res[testFile] = projSettings;
                }
            }
            return res;
        }

        public static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StringReader(xml), settings));
        }
    }
}
