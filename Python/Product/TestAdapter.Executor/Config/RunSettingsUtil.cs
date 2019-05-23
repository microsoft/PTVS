using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;

namespace Microsoft.PythonTools.TestAdapter.Config {
    public class RunSettingsUtil {
        public static Dictionary<string, PythonProjectSettings> GetSourceToProjSettings(IRunSettings settings) {
            var doc = Read(settings.SettingsXml);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/TestCases/Project");
            Dictionary<string, PythonProjectSettings> res = new Dictionary<string, PythonProjectSettings>();

            foreach (XPathNavigator project in nodes) {
                PythonProjectSettings projSettings = new PythonProjectSettings(
                    project.GetAttribute("home", ""),
                    project.GetAttribute("workingDir", ""),
                    project.GetAttribute("interpreter", ""),
                    project.GetAttribute("pathEnv", ""),
                    project.GetAttribute("nativeDebugging", "").IsTrue()
                );

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
                    res[testFile] = projSettings;
                }
            }
            return res;
        }


        public static Dictionary<string, PythonProjectSettings> GetProjectSettings(IRunSettings settings) {
            var doc = Read(settings.SettingsXml);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/RunSettings/Python/TestCases/Project");
            Dictionary<string, PythonProjectSettings> res = new Dictionary<string, PythonProjectSettings>();

            foreach (XPathNavigator project in nodes) {
                PythonProjectSettings projSettings = new PythonProjectSettings(
                    project.GetAttribute("home", ""),
                    project.GetAttribute("workingDir", ""),
                    project.GetAttribute("interpreter", ""),
                    project.GetAttribute("pathEnv", ""),
                    project.GetAttribute("nativeDebugging", "").IsTrue()
                );

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
                    projSettings.Sources.Add(testFile);
                }

                res[projSettings.ProjectHome] = projSettings;
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
