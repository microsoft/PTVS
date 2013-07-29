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
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;

namespace Microsoft {
    static class SettingsManagerCreator {
        public static SettingsManager GetSettingsManager(IServiceProvider provider) {
            SettingsManager settings = null;
            string devenvPath = null;
            if (provider == null) {
                provider = ServiceProvider.GlobalProvider;
            }

            if (provider != null) {
                try {
                    settings = new ShellSettingsManager(provider);
                } catch (NotSupportedException) {
                    var dte = (DTE)provider.GetService(typeof(DTE));
                    if (dte != null) {
                        devenvPath = dte.FullName;
                    }
                }
            }

            if (settings == null) {
                if (!File.Exists(devenvPath)) {
                    using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (var key = root.OpenSubKey(string.Format(@"Software\Microsoft\VisualStudio\{0}\Setup\VS", AssemblyVersionInfo.VSVersion))) {
                        if (key == null) {
                            throw new InvalidOperationException("Cannot find settings store for Visual Studio " + AssemblyVersionInfo.VSVersion);
                        }
                        devenvPath = key.GetValue("EnvironmentPath") as string;
                    }
                }
                if (!File.Exists(devenvPath)) {
                    throw new InvalidOperationException("Cannot find settings store for Visual Studio " + AssemblyVersionInfo.VSVersion);
                }
#if DEBUG
                settings = ExternalSettingsManager.CreateForApplication(devenvPath, "Exp");
#else
                settings = ExternalSettingsManager.CreateForApplication(devenvPath);
#endif
            }

            return settings;
        }
    }
}
