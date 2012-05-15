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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
            // enable FastCGI in IIS
            var proc = Process.Start(
                Path.Combine(Environment.GetEnvironmentVariable("windir"), "System32\\PkgMgr.exe"),
                "/iu:IIS-WebServerRole;IIS-WebServer;IIS-CommonHttpFeatures;IIS-StaticContent;IIS-DefaultDocument;IIS-DirectoryBrowsing;IIS-HttpErrors;IIS-HealthAndDiagnostics;IIS-HttpLogging;IIS-LoggingLibraries;IIS-RequestMonitor;IIS-Security;IIS-RequestFiltering;IIS-HttpCompressionStatic;IIS-WebServerManagementTools;IIS-ManagementConsole;WAS-WindowsActivationService;WAS-ProcessModel;WAS-NetFxEnvironment;WAS-ConfigurationAPI;IIS-CGI"
            );
            proc.WaitForExit();

            // Crack RoleModel.xml to figure out where our site lives...
            // Path.GetFullPath - in the cloud the RoleRoot is "E:" instead of "E:\"
            var roleRoot = Environment.GetEnvironmentVariable("RoleRoot");
            if (!roleRoot.EndsWith("\\")) {
                roleRoot = roleRoot + "\\";
            }

            string interpreter = null;
            try {
                interpreter = RoleEnvironment.GetConfigurationSettingValue("Microsoft.PythonTools.Azure.PythonInterpreter");
            } catch {
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

            if (!Path.IsPathRooted(physicalDir)) {
                physicalDir = Path.Combine(roleRoot, physicalDir);
            }

            nodes = navigator.Select("/sd:RoleModel/sd:Properties/sd:Property", mngr);
            bool isDebug = false;
            foreach (XPathNavigator node in nodes) {
                if (node.GetAttribute("name", "") == "Configuration" &&
                    node.GetAttribute("value", "") == "Debug") {
                    isDebug = true;
                }
            }

            if (physicalDir != null) {
                string fastCgiPath = Path.Combine(physicalDir, "bin\\wfastcgi.py");
                string webpiCmdLinePath = Path.Combine(physicalDir, "bin\\WebPICmdLine.exe");
                string settingsName = null, pythonPath = null;
                string setupCfg = Path.Combine(physicalDir, "bin\\AzureSetup.cfg");
                List<string> webpiInstalls = new List<string>();
                if (File.Exists(setupCfg)) {
                    try {
                        var allLines = File.ReadAllLines(setupCfg);
                        foreach (var line in allLines) {
                            var curOptions = line.Split(new[] { '=' }, 2);
                            if (curOptions.Length == 2) {
                                switch(curOptions[0]) {
                                    case "settings_module":
                                        settingsName = curOptions[1];
                                        break;
                                    case "python_path":                                        
                                        pythonPath = Environment.ExpandEnvironmentVariables(
                                            Regex.Replace(
                                                curOptions[1], 
                                                Regex.Escape("%RootDir%"), 
                                                Regex.Escape(physicalDir), 
                                                RegexOptions.IgnoreCase
                                            )
                                        );
                                        break;
                                    case "interpreter_path":
                                        interpreter = Environment.ExpandEnvironmentVariables(
                                            Regex.Replace(
                                                curOptions[1],
                                                Regex.Escape("%RootDir%"),
                                                Regex.Escape(physicalDir),
                                                RegexOptions.IgnoreCase
                                            )
                                        );
                                        break;
                                    case "webpi_install":
                                        webpiInstalls.Add(
                                            curOptions[1]
                                        );
                                        break;
                                }
                            }
                        }
                    } catch (IOException) {
                    }
                }

                // setup any installed products via WebPI...
                foreach (var install in webpiInstalls) {
                    var paths = install.Split(new[] { ';' }, 2);
                    if (paths.Length == 2) {
                        var psi = new ProcessStartInfo(
                            webpiCmdLinePath,
                            "\"/Feeds:" + paths[0] + "\" " +
                            "\"/Products: " + paths[1] + "\""
                        );
                        var process = Process.Start(psi);
                        process.WaitForExit();
                    }
                }

                using (ServerManager serverManager = new ServerManager()) {
                    Configuration config = serverManager.GetApplicationHostConfiguration();

                    ConfigurationSection fastCgiSection = config.GetSection("system.webServer/fastCgi");
                    ConfigurationElementCollection fastCgiCollection = fastCgiSection.GetCollection();

                    if (String.IsNullOrEmpty(interpreter)) {
                        // TODO: Better discovery....
                        interpreter = Path.Combine(
                            Environment.GetEnvironmentVariable("SystemDrive") + "\\",
                            "Python27\\python.exe"
                        );
                    }

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
                    applicationElement["instanceMaxRequests"] = isDebug ? 1 : 10000;
                    applicationElement["protocol"] = "NamedPipe";
                    applicationElement["flushNamedPipe"] = false;

                    ConfigurationElementCollection environmentVariablesCollection = applicationElement.GetCollection("environmentVariables");
                    if (settingsName == null) {
                        if (physicalDir.Length > 0 && physicalDir[physicalDir.Length - 1] == Path.DirectorySeparatorChar) {
                            settingsName = Path.GetFileName(physicalDir.Substring(0, physicalDir.Length - 1));
                        } else {
                            settingsName = Path.GetFileName(physicalDir);
                        }
                        settingsName += ".settings";
                    }

                    if (pythonPath == null) {
                        pythonPath = Path.Combine(physicalDir, "..");
                    }


                    foreach (var envVar in new[] { 
                        new { Name = "DJANGO_SETTINGS_MODULE", Value = settingsName },
                        new { Name = "PYTHONPATH", Value = pythonPath } 
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
