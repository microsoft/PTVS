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

using Microsoft.PythonTools.Options;

namespace Microsoft.PythonTools.Project
{
    class VsPackageManagerUI : IPackageManagerUI
    {
        private readonly IServiceProvider _site;
        private readonly Redirector _outputWindow;
        private readonly GeneralOptions _options;
        private readonly bool _alwaysElevate;

        public VsPackageManagerUI(IServiceProvider provider, bool alwaysElevate = false)
        {
            _site = provider;
            _outputWindow = OutputWindowRedirector.GetGeneral(provider);
            _options = provider.GetPythonToolsService().GeneralOptions;
            _alwaysElevate = alwaysElevate;
        }

        public void OnErrorTextReceived(IPackageManager sender, string text)
        {
            _outputWindow.WriteErrorLine(text.TrimEndNewline());
        }

        public void OnOperationFinished(IPackageManager sender, string operation, bool success)
        {
            if (_options.ShowOutputWindowForPackageInstallation)
            {
                _outputWindow.ShowAndActivate();
            }
        }

        public void OnOperationStarted(IPackageManager sender, string operation)
        {
            if (_options.ShowOutputWindowForPackageInstallation)
            {
                _outputWindow.ShowAndActivate();
            }
        }

        public void OnOutputTextReceived(IPackageManager sender, string text)
        {
            _outputWindow.WriteLine(text.TrimEndNewline());
        }

        public async Task<bool> ShouldElevateAsync(IPackageManager sender, string operation)
        {
            if (_alwaysElevate)
            {
                return true;
            }

            return ShouldElevate(_site, sender.Factory.Configuration, operation);
        }

        public static bool ShouldElevate(IServiceProvider site, InterpreterConfiguration config, string operation)
        {
            var opts = site.GetPythonToolsService().GeneralOptions;
            if (opts.ElevatePip)
            {
                return true;
            }

            try
            {
                // Create a test file and delete it immediately to ensure we can do it.
                // If this fails, prompt the user to see whether they want to elevate.
                var testFile = PathUtils.GetAvailableFilename(config.GetPrefixPath(), "access-test", ".txt");
                using (new FileStream(testFile, FileMode.CreateNew, FileAccess.Write, FileShare.Delete, 4096, FileOptions.DeleteOnClose)) { }
                return false;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            var td = new TaskDialog(site)
            {
                Title = Strings.ProductTitle,
                MainInstruction = Strings.ElevateForInstallPackage_MainInstruction,
                AllowCancellation = true,
            };
            var elevate = new TaskDialogButton(Strings.ElevateForInstallPackage_Elevate, Strings.ElevateForInstallPackage_Elevate_Note)
            {
                ElevationRequired = true
            };
            var noElevate = new TaskDialogButton(Strings.ElevateForInstallPackage_DoNotElevate, Strings.ElevateForInstallPackage_DoNotElevate_Note);
            var elevateAlways = new TaskDialogButton(Strings.ElevateForInstallPackage_ElevateAlways, Strings.ElevateForInstallPackage_ElevateAlways_Note)
            {
                ElevationRequired = true
            };
            td.Buttons.Add(elevate);
            td.Buttons.Add(noElevate);
            td.Buttons.Add(elevateAlways);
            td.Buttons.Add(TaskDialogButton.Cancel);
            var sel = td.ShowModal();
            if (sel == TaskDialogButton.Cancel)
            {
                throw new OperationCanceledException();
            }

            if (sel == noElevate)
            {
                return false;
            }

            if (sel == elevateAlways)
            {
                opts.ElevatePip = true;
                opts.Save();
            }

            return true;
        }
    }
}
