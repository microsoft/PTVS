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

using System.ComponentModel.Composition;
using TestUtilities.SharedProject;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.Nodejs.Tests.UI {
    [Export(typeof(IProjectProcessor))]
    [ProjectExtension(".pyproj")]
    public class PythonProjectProcessor : IProjectProcessor {
        public void PreProcess(MSBuild.Project project) {
            project.SetProperty("ProjectHome", ".");
            project.SetProperty("WorkingDirectory", ".");

            project.Xml.AddProperty("VisualStudioVersion", "11.0").Condition = "'$(VisualStudioVersion)' == ''";
            project.Xml.AddProperty("PtvsTargetsFile", "$(MSBuildExtensionsPath32)\\Microsoft\\VisualStudio\\v$(VisualStudioVersion)\\Python Tools\\Microsoft.PythonTools.targets");

            var import1 = project.Xml.AddImport("$(PtvsTargetsFile)");
            import1.Condition = "Exists($(PtvsTargetsFile))";
            var import2 = project.Xml.AddImport("$(MSBuildToolsPath)\\Microsoft.Common.targets");
            import2.Condition = "!Exists($(PtvsTargetsFile))";
        }

        public void PostProcess(MSBuild.Project project) {
        }
    }
}
