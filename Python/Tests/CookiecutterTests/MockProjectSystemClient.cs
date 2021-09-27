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

namespace CookiecutterTests
{
    class MockProjectSystemClient : IProjectSystemClient
    {
        public List<Tuple<ProjectLocation, CreateFilesOperationResult>> Added { get; } = new List<Tuple<ProjectLocation, CreateFilesOperationResult>>();

        public bool IsSolutionOpen { get; set; }

#pragma warning disable CS0067
        public event EventHandler SolutionOpenChanged;
#pragma warning restore CS0067

        public void AddToProject(ProjectLocation location, CreateFilesOperationResult creationResult)
        {
            Added.Add(Tuple.Create(location, creationResult));
        }

        public void AddToSolution(string projectFilePath)
        {
            throw new NotImplementedException();
        }

        public ProjectLocation GetSelectedFolderProjectLocation()
        {
            throw new NotImplementedException();
        }

        public void OpenSolution()
        {
            IsSolutionOpen = true;
            SolutionOpenChanged?.Invoke(this, EventArgs.Empty);
        }

        public void CloseSolution()
        {
            IsSolutionOpen = false;
            SolutionOpenChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
