/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace AzureSetup {
    class Program {
        static void Main(string[] args) {
            ConfigureFastCgi();
        }

        public static void ConfigureFastCgi() {
            // Crack RoleModel.xml to figure out where our site lives...
            var roleRoot = Environment.GetEnvironmentVariable("RoleRoot");
            string interpreter;
            try {
                interpreter = RoleEnvironment.GetConfigurationSettingValue("Microsoft.PythonTools.Azure.PythonInterpreter");
            } catch {
                interpreter = "C:\\Python27\\python.exe";
            }
            if (String.IsNullOrEmpty(interpreter)) {
                // TODO: Better discovery....
                interpreter = "C:\\Python27\\python.exe";
            }

            var doc = new XPathDocument(Path.Combine(roleRoot, "RoleModel.xml"));

            var navigator = doc.CreateNavigator();
            XmlNamespaceManager mngr = new XmlNamespaceManager(new NameTable());
            mngr.AddNamespace("sd", "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition");

            var nodes = navigator.Select("/sd:RoleModel/sd:Sites/sd:Site", mngr);
            string physicalDir = null;
            foreach (XPathNavigator node in nodes) {
                // TODO: Multiple sites?
                physicalDir = node.GetAttribute("physicalDirectory", "");
                break;
            }

            if (physicalDir != null) {
                string fastCgiPath = Path.Combine(physicalDir, "bin\\wfastcgi.py");

                using (ServerManager serverManager = new ServerManager()) {
                    Configuration config = serverManager.GetApplicationHostConfiguration();

                    ConfigurationSection fastCgiSection = config.GetSection("system.webServer/fastCgi");
                    ConfigurationElementCollection fastCgiCollection = fastCgiSection.GetCollection();

                    // remove the previous entry if we're already registered at the same path...
                    foreach (var child in fastCgiCollection) {
                        var path = child.Attributes["fullPath"];
                        var arguments = child.Attributes["arguments"];

                        if ((string)path.Value == interpreter && (string)arguments.Value == fastCgiPath) {
                            fastCgiCollection.Remove(child);
                            break;
                        }
                    }

                    ConfigurationElement applicationElement = fastCgiCollection.CreateElement("application");

                    applicationElement["fullPath"] = interpreter;
                    applicationElement["arguments"] = fastCgiPath;
                    applicationElement["maxInstances"] = 4;
                    applicationElement["idleTimeout"] = 300;
                    applicationElement["activityTimeout"] = 30;
                    applicationElement["requestTimeout"] = 90;
                    applicationElement["instanceMaxRequests"] = 10000;
                    applicationElement["protocol"] = "NamedPipe";
                    applicationElement["flushNamedPipe"] = false;

                    ConfigurationElementCollection environmentVariablesCollection = applicationElement.GetCollection("environmentVariables");
                    string settingsName;
                    if (physicalDir.Length > 0 && physicalDir[physicalDir.Length - 1] == Path.DirectorySeparatorChar) {
                        settingsName = Path.GetFileName(physicalDir.Substring(0, physicalDir.Length - 1));
                    } else {
                        settingsName = Path.GetFileName(physicalDir);
                    }
                    settingsName += ".settings";

                    foreach (var envVar in new[] { 
                    new { Name = "DJANGO_SETTINGS_MODULE", Value = settingsName },
                    new { Name = "PYTHONPATH", Value = Path.Combine(physicalDir, "..") } 
                    }
                    ) {
                        ConfigurationElement environmentVariableElement = environmentVariablesCollection.CreateElement("environmentVariable");
                        environmentVariableElement["name"] = envVar.Name;
                        environmentVariableElement["value"] = envVar.Value;
                        environmentVariablesCollection.Add(environmentVariableElement);
                    }

                    fastCgiCollection.Add(applicationElement);


                    serverManager.CommitChanges();
                }

                // patch web.config w/ the correct path to our fast cgi script
                var webConfig = Path.Combine(physicalDir, "web.config");
                var text = File.ReadAllText(webConfig);
                File.WriteAllText(webConfig, text.Replace("WFASTCGIPATH", fastCgiPath).Replace("INTERPRETERPATH", interpreter));
            }
        }
    }
}
