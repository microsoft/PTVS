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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Win32;

namespace CanopyInterpreter {
    /// <summary>
    /// Provides interpreter factory objects for Canopy.
    /// 
    /// The factory provider is responsible for detecting installations of
    /// Canopy and managing IPythonInterpreterFactory objects for each item to
    /// display to the user.
    /// 
    /// For Canopy, we create two IPythonInterpreterFactory objects, but only
    /// return one to the user. The CanopyInterpreterFactory object represents
    /// the User directory, and contains a default factory representing App.
    /// </summary>
    [InterpreterFactoryId("Canopy")]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    class CanopyInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
        private readonly List<IPythonInterpreterFactory> _interpreters;
        const string CanopyCorePath = @"Software\Enthought\Canopy";
        const string InstallPath = "InstalledPath";
        const string CanopyVersionKey = "Version";

        public CanopyInterpreterFactoryProvider() {
            _interpreters = new List<IPythonInterpreterFactory>();
            DiscoverInterpreterFactories();

            try {
                // Watch for changes in the Canopy core registry key
                RegistryWatcher.Instance.Add(
                    RegistryHive.CurrentUser,
                    RegistryView.Default,
                    CanopyCorePath,
                    Registry_Changed,
                    recursive: true,
                    notifyValueChange: true,
                    notifyKeyChange: true
                );
            } catch (ArgumentException) {
                // Watch HKCU\Software for the Canopy key to be created
                RegistryWatcher.Instance.Add(
                    RegistryHive.CurrentUser,
                    RegistryView.Default,
                    "Software",
                    Registry_Software_Changed,
                    recursive: false,
                    notifyValueChange: false,
                    notifyKeyChange: true
                );
            }
        }

        private void Registry_Changed(object sender, RegistryChangedEventArgs e) {
            DiscoverInterpreterFactories();
        }

        private void Registry_Software_Changed(object sender, RegistryChangedEventArgs e) {
            using (var root = RegistryKey.OpenBaseKey(e.Hive, e.View))
            using (var key = root.OpenSubKey(CanopyCorePath)) {
                if (key != null) {
                    Registry_Changed(sender, e);
                    e.CancelWatcher = true;
                    RegistryWatcher.Instance.Add(e.Hive, e.View, CanopyCorePath, Registry_Changed,
                        recursive: true, notifyValueChange: true, notifyKeyChange: true);
                }
            }
        }
        /// <summary>
        /// Reads the details of Canopy from the registry and adds any new
        /// interpreters to <see cref="_interpreters"/>.
        /// </summary>
        /// <param name="canopyKey">The base Canopy registry key.</param>
        /// <returns>
        /// True if an interpreter was added; otherwise, false.
        /// </returns>
        private bool ReadInterpreterFactory(RegistryKey canopyKey) {
            var installPath = canopyKey.GetValue(InstallPath) as string;
            if (!Directory.Exists(installPath)) {
                // Canopy is not installed
                return false;
            }

            // TODO: Read the User path from the registry.
            var userPath = Path.Combine(installPath, @"Canopy\User");

            if (!Directory.Exists(userPath)) {
                // TODO: Bootstrap Canopy's virtual environment
                // Bear in mind that this function is called close to VS startup
                // and the user may not be interested in creating the
                // environment now.
                // This function is also called when the Canopy registry key
                // changes, so setting the User path key after bootstrapping
                // will cause the interpreter to be discovered.
                return false;
            }

            string basePath, versionStr;
            ReadPyVEnvCfg(userPath, out basePath, out versionStr);
            if (!Directory.Exists(basePath)) {
                // User path has an invalid home path set.
                return false;
            }

            Version version;
            if (Version.TryParse(versionStr, out version)) {
                version = new Version(version.Major, version.Minor);
            } else {
                version = new Version(2, 7);
            }

            var canopyVersion = canopyKey.GetValue(CanopyVersionKey) as string;

            try {
                var baseFactory = CanopyInterpreterFactory.CreateBase(basePath, canopyVersion, version);
                var factory = CanopyInterpreterFactory.Create(baseFactory, userPath, canopyVersion);

                if (!_interpreters.Any(f => f.Configuration.Id == factory.Configuration.Id)) {
                    _interpreters.Add(factory);
                    return true;
                }
            } catch (Exception) {
                // TODO: Report failure to create factory
            }

            return false;
        }

        /// <summary>
        /// Reads a pyvenv.cfg file at <paramref name="prefixPath"/> and returns
        /// the values specified for 'home' and 'version'.
        /// </summary>
        /// <param name="prefixPath">
        /// A path containing a pyvenv.cfg file.
        /// </param>
        /// <param name="home">
        /// On return, contains the value for 'home' or null.
        /// </param>
        /// <param name="version">
        /// On return, contains the value for 'version' or null.
        /// </param>
        private static void ReadPyVEnvCfg(string prefixPath, out string home, out string version) {
            home = null;
            version = null;

            try {
                foreach (var line in File.ReadLines(Path.Combine(prefixPath, "pyvenv.cfg"))) {
                    int equal = line.IndexOf('=');
                    if (equal < 0) {
                        continue;
                    }

                    var name = line.Substring(0, equal).Trim();
                    var value = line.Substring(equal + 1).Trim();

                    if (name == "home") {
                        home = value;
                    } else if (name == "version") {
                        version = value;
                    }
                }
            } catch (IOException) {
            } catch (UnauthorizedAccessException) {
            } catch (SecurityException) {
            }
        }

        /// <summary>
        /// Called on initialize and on registry change events to discover new
        /// interpreters.
        /// </summary>
        /// <remarks>
        /// This function does not remove interpreters from PTVS when they are
        /// uninstalled. PTVS should be closed before uninstalling interpreters
        /// to ensure that files are no longer in use.
        /// </remarks>
        private void DiscoverInterpreterFactories() {
            bool anyAdded = false;

            var arch = Environment.Is64BitOperatingSystem ? null : (ProcessorArchitecture?)ProcessorArchitecture.X86;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var canopyKey = baseKey.OpenSubKey(CanopyCorePath)) {
                if (canopyKey != null) {
                    anyAdded = ReadInterpreterFactory(canopyKey);
                }
            }

            if (anyAdded) {
                OnInterpreterFactoriesChanged();
            }
        }


        #region IPythonInterpreterProvider Members

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            return _interpreters;
        }

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            return GetInterpreterFactories().Select(x => x.Configuration);
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            return GetInterpreterFactories()
                .Where(x => x.Configuration.Id == id)
                .FirstOrDefault();
        }

        public object GetProperty(string id, string propName) => null;

        public event EventHandler InterpreterFactoriesChanged;

        private void OnInterpreterFactoriesChanged() {
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
