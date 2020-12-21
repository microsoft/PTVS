// Visual Studio Shared Project
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
using System.IO;
using TestUtilities.SharedProject;
using MSBuild = Microsoft.Build.Evaluation;

namespace TestUtilities
{
    public sealed class SolutionFolder : ISolutionElement
    {
        private readonly string _name;
        private static Guid _solutionFolderGuid = new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8");

        public SolutionFolder(string name)
        {
            _name = name;
        }
        public MSBuild.Project Save(MSBuild.ProjectCollection collection, string location)
        {
            Directory.CreateDirectory(Path.Combine(location, _name));
            return null;
        }

        public Guid TypeGuid
        {
            get { return _solutionFolderGuid; }
        }

        public SolutionElementFlags Flags
        {
            get { return SolutionElementFlags.ExcludeFromConfiguration; }
        }

        public string Name { get { return _name; } }
    }
}
