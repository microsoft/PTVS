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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    class DefaultPythonProject : IPythonProject
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _filePath;

        public event EventHandler<AnalyzerChangingEventArgs> ProjectAnalyzerChanging { add { } remove { } }

        public DefaultPythonProject(IServiceProvider serviceProvider, string filePath)
        {
            Utilities.ArgumentNotNullOrEmpty("filePath", filePath);
            _filePath = filePath;
            _serviceProvider = serviceProvider;
        }

        public void SetProperty(string name, string value)
        {
            Debug.Fail("Unexpected DefaultPythonProject.SetProperty() call");
        }

        public Projects.ProjectAnalyzer GetProjectAnalyzer()
        {
            return _serviceProvider.GetPythonToolsService().TryGetSharedAnalyzer(null, out _, addUser: false);
        }

        public IPythonInterpreterFactory GetInterpreterFactory()
        {
            return _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>().DefaultInterpreter;
        }

        public bool Publish(PublishProjectOptions options)
        {
            Debug.Fail("Unexpected DefaultPythonProject.Publish() call");
            return false;
        }

        private string FullPath => Path.GetFullPath(_filePath);
        public string GetProperty(string name) => null;
        public string GetWorkingDirectory() => PathUtils.GetParent(FullPath);
        public string GetStartupFile() => FullPath;
        public string ProjectDirectory => PathUtils.GetParent(_filePath);
        public string ProjectName => Path.GetFileNameWithoutExtension(_filePath);
        public string ProjectHome => ProjectDirectory;
        public string ProjectFile => FullPath;
        public IServiceProvider Site => _serviceProvider;
        public string GetUnevaluatedProperty(string name) => null;
        public IAsyncCommand FindCommand(string canonicalName) => null;
        public ProjectInstance GetMSBuildProjectInstance() => null;
        public void AddActionOnClose(object key, Action<object> action) { }
        public IPythonInterpreterFactory GetInterpreterFactoryOrThrow() => GetInterpreterFactory();
        public LaunchConfiguration GetLaunchConfigurationOrThrow() => new LaunchConfiguration(GetInterpreterFactory().Configuration);

        public event EventHandler ProjectAnalyzerChanged { add { } remove { } }
        public void SetOrAddPropertyAfter(string name, string value, string afterProperty) { }
    }
}
