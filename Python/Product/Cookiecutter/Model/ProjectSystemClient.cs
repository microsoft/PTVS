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

using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Model {
    class ProjectSystemClient : IProjectSystemClient {
        private readonly EnvDTE80.DTE2 _dte;
        private readonly SolutionEvents _solutionEvents;

        private static readonly HashSet<Guid> UnsupportedProjectKinds = new HashSet<Guid>() {
            new Guid("cc5fd16d-436d-48ad-a40c-5a424c6e3e79"), // Azure Cloud Service
        };

        public ProjectSystemClient(EnvDTE80.DTE2 dte) {
            _dte = dte;
            _solutionEvents = dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += OnSolutionChanged;
            _solutionEvents.Opened += OnSolutionChanged;
        }

        public event EventHandler SolutionOpenChanged;

        public bool IsSolutionOpen {
            get {
                return _dte.Solution.IsOpen;
            }
        }

        public ProjectLocation GetSelectedFolderProjectLocation() {
            try {
                var locations = GetSelectedItemPaths().ToArray();
                if (locations.Length == 1) {
                    var p = locations[0];
                    return Directory.Exists(p.FolderPath) ? p : null;
                }
            } catch (Exception e) when (!e.IsCriticalException()) {
            }

            return null;
        }

        public void AddToProject(ProjectLocation location, CreateFilesOperationResult creationResult) {
            string folderPath = location.FolderPath;
            string targetProjectUniqueName = location.ProjectUniqueName;

            var project = FindProject(targetProjectUniqueName);
            if (project == null) {
                throw new ArgumentException(Strings.ProjectNotFound.FormatUI(targetProjectUniqueName), nameof(targetProjectUniqueName));
            }

            var parentItems = GetTargetProjectItems(project, folderPath);

            // Remember which folder items we're adding, because we can't query them
            // in F# project system
            var folderItems = new Dictionary<string, EnvDTE.ProjectItems>();
            try {
                foreach (var createdFolderPath in creationResult.FoldersCreated) {
                    var absoluteFilePath = Path.Combine(folderPath, createdFolderPath);
                    var folder = GetOrCreateFolderItem(parentItems, createdFolderPath);
                    folderItems[createdFolderPath] = folder;
                }
            } catch (NotImplementedException) {
                // Some project types such as C++ don't support creating folders
            }

            foreach (var createdFilePath in creationResult.FilesCreated) {
                var absoluteFilePath = Path.Combine(folderPath, createdFilePath);
                EnvDTE.ProjectItems itemParent;
                try {
                    itemParent = GetOrCreateFolderItem(parentItems, Path.GetDirectoryName(createdFilePath));
                } catch (NotImplementedException) {
                    // Some project types such as C++ don't support creating folders
                    // so we'll add everything flat
                    itemParent = parentItems;
                } catch (ArgumentException) {
                    // Some project types such as F# don't return folders as ProjectItem
                    // so we can't find the folder we just created above. Attempting to
                    // create it generates a ArgumentException saying the folder already exists.
                    if (!folderItems.TryGetValue(Path.GetDirectoryName(createdFilePath), out itemParent)) {
                        itemParent = parentItems;
                    }
                }

                if (FindItemByName(itemParent, Path.GetFileName(createdFilePath)) == null) {
                    itemParent.AddFromFile(absoluteFilePath);
                }
            }
        }

        public void AddToSolution(string projectFilePath) {
            _dte.Solution.AddFromFile(projectFilePath);
        }

        private EnvDTE.Project FindProject(string projectUniqueName) {
            var dte = _dte;
            var items = (Array)dte.ToolWindows.SolutionExplorer.SelectedItems;
            foreach (var proj in dte.ActiveSolutionProjects) {
                var p = proj as EnvDTE.Project;
                if (p != null && p.UniqueName == projectUniqueName) {
                    return p;
                }
            }
            return null;
        }

        private EnvDTE.ProjectItems GetOrCreateFolderItem(EnvDTE.ProjectItems parentItems, string folderPath) {
            var relativeFolderParts = folderPath.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string relativefolderPart in relativeFolderParts) {
                var folderItem = FindItemByName(parentItems, relativefolderPart);
                if (folderItem == null) {
                    folderItem = parentItems.AddFolder(relativefolderPart);
                }
                parentItems = folderItem.ProjectItems;
            }

            return parentItems;
        }

        private EnvDTE.ProjectItems GetTargetProjectItems(EnvDTE.Project p, string folderPath) {
            var projectFolderPath = GetProjectFolder(p);
            var relativeParentFolder = PathUtils.GetRelativeDirectoryPath(projectFolderPath, folderPath);
            var relativeParentParts = relativeParentFolder.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var parentItems = p.ProjectItems;
            foreach (var relativeParentPart in relativeParentParts) {
                var parentItem = FindItemByName(parentItems, relativeParentPart);
                if (parentItem != null) {
                    parentItems = parentItem.ProjectItems;
                } else {
                    return null;
                }
            }
            return parentItems;
        }

        private EnvDTE.ProjectItem FindItemByName(EnvDTE.ProjectItems projectItems, string name) {
            foreach (EnvDTE.ProjectItem item in projectItems) {
                if (item.Name == name) {
                    return item;
                }
            }

            return null;
        }

        private IEnumerable<ProjectLocation> GetSelectedItemPaths() {
            var items = (Array)_dte.ToolWindows.SolutionExplorer.SelectedItems;
            foreach (EnvDTE.UIHierarchyItem selItem in items) {
                var item = selItem.Object as EnvDTE.ProjectItem;
                if (item != null && item.Properties != null) {
                    if (IsProjectSupported(item.ContainingProject)) {
                        yield return new ProjectLocation() {
                            FolderPath = item.Properties.Item("FullPath").Value.ToString(),
                            ProjectKind = item.ContainingProject.Kind,
                            ProjectUniqueName = item.ContainingProject.UniqueName,
                        };
                    }
                }

                var proj = selItem.Object as EnvDTE.Project;
                if (proj != null && IsProjectSupported(proj)) {
                    var projFolder = GetProjectFolder(proj);
                    if (!string.IsNullOrEmpty(projFolder)) {
                        yield return new ProjectLocation() {
                            FolderPath = projFolder,
                            ProjectKind = proj.Kind,
                            ProjectUniqueName = proj.UniqueName,
                        };
                    }
                }
            }
        }

        private static bool IsProjectSupported(EnvDTE.Project proj) {
            Guid guid;
            if (Guid.TryParse(proj.Kind, out guid)) {
                if (UnsupportedProjectKinds.Contains(guid)) {
                    return false;
                }
            }

            return true;
        }

        private static string GetProjectFolder(EnvDTE.Project proj) {
            try {
                // Python and C# projects
                if (proj.Properties != null) {
                    return proj.Properties.Item("FullPath").Value.ToString();
                }
            } catch (ArgumentException) {
                // C++ project
                try {
                    if (proj.Object != null) {
                        return proj.Object.ProjectDirectory;
                    }
                } catch (Exception) {
                }
            }

            return null;
        }

        private void OnSolutionChanged() {
            SolutionOpenChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
