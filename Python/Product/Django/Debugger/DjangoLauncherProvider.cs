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

namespace Microsoft.PythonTools.Django.Debugger
{
    [Export(typeof(IPythonLauncherProvider))]
    class DjangoLauncherProvider : IPythonLauncherProvider
    {
        internal readonly IEnumerable<Lazy<IPythonLauncherProvider>> _providers;

        [ImportingConstructor]
        public DjangoLauncherProvider([ImportMany] IEnumerable<Lazy<IPythonLauncherProvider>> allProviders)
        {
            _providers = allProviders;
        }

        #region IPythonLauncherProvider Members

        public IPythonLauncherOptions GetLauncherOptions(IPythonProject properties)
        {
            return new PythonWebLauncherOptions(properties);
        }

        public string Name
        {
            get { return "Django launcher"; }
        }

        public string LocalizedName
        {
            get { return Resources.DjangoLauncherName; }
        }

        public int SortPriority
        {
            get { return 200; }
        }

        public string Description
        {
            get { return Resources.DjangoLauncherDescription; }
        }

        public IProjectLauncher CreateLauncher(IPythonProject project)
        {
            var webLauncher = _providers.FirstOrDefault(p => p.Value.Name == PythonConstants.WebLauncherName);

            if (webLauncher == null)
            {
                throw new InvalidOperationException(Resources.CannotFindPythonWebLauncher);
            }

            return webLauncher.Value.CreateLauncher(project);
        }

        #endregion
    }
}
