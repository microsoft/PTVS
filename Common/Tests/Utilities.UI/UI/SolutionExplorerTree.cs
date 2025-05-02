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
using System.Linq;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI {
    public class SolutionExplorerTree : TreeView {
        public SolutionExplorerTree(AutomationElement element)
            : base(element) {
        }

        public void AssertFileExists(string projectLocation, params string[] path) {
            AssertItemExistsInTree(path);

            var basePath = projectLocation;
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsTrue(File.Exists(basePath), "File doesn't exist: " + basePath);
        }

        public void AssertFileExistsWithContent(string projectLocation, string content, params string[] path) {
            AssertItemExistsInTree(path);

            var basePath = projectLocation;
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsTrue(File.Exists(basePath), "File doesn't exist: " + basePath);
            Assert.AreEqual(File.ReadAllText(basePath), content);
        }

        public void AssertFileDoesntExist(string projectLocation, params string[] path) {
            Assert.IsNull(FindItem(path), "Item exists in solution explorer: " + String.Join("\\", path));

            var basePath = projectLocation;
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsFalse(File.Exists(basePath), "File exists: " + basePath);
        }

        public void AssertFolderExists(string projectLocation, params string[] path) {
            AssertItemExistsInTree(path);

            var basePath = projectLocation;
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsTrue(Directory.Exists(basePath), "Folder doesn't exist: " + basePath);
        }

        public void AssertFolderDoesntExist(string projectLocation, params string[] path) {
            Assert.IsNull(WaitForItemRemoved(path), "Item exists in solution explorer: " + String.Join("\\", path));

            var basePath = projectLocation;
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsFalse(Directory.Exists(basePath), "Folder exists: " + basePath);
        }

        private void AssertItemExistsInTree(string[] path) {
            var item = WaitForItem(path);
            if (item == null) {
                string msg = "Item not found in solution explorer " + String.Join("\\", path);
                for (int i = 1; i < path.Length; i++) {
                    item = FindItem(path.Take(i).ToArray());
                    if (item == null) {
                        msg += Environment.NewLine + "Item missing at: " + String.Join("\\", path.Take(i));
                        break;
                    }
                }
                Assert.IsNotNull(item, msg);
            }
        }

        public void SelectProject(EnvDTE.Project project) {
            var slnName = string.Format("Solution '{0}' ",
                Path.GetFileNameWithoutExtension(project.DTE.Solution.FullName)                
            );
            var item = WaitForItem(slnName, project.Name).AsWrapper();
            Assert.IsNotNull(item);
            item.Select();
        }

        public TreeNode WaitForChildOfProject(EnvDTE.Project project, params string[] path) {
            return WaitForChildOfProject(project, TimeSpan.FromSeconds(10), path);
        }

        public TreeNode WaitForChildOfProject(EnvDTE.Project project, TimeSpan timeout, params string[] path) {
            var item = WaitForItemHelper(p => FindChildOfProjectHelper(project, p, false), path, timeout);
            // Check one more time, but now let the assertions be raised.
            return new TreeNode(FindChildOfProjectHelper(project, path, true));
        }

        public AutomationElement WaitForChildOfProjectRemoved(EnvDTE.Project project, params string[] path) {
            return WaitForItemRemovedHelper(p => FindChildOfProjectHelper(project, p, false), path);
        }

        public TreeNode FindChildOfProject(EnvDTE.Project project, params string[] path) {
            return new TreeNode(FindChildOfProjectHelper(project, path, true));
        }

        public TreeNode TryFindChildOfProject(EnvDTE.Project project, params string[] path) {
            return new TreeNode(FindChildOfProjectHelper(project, path, false));
        }

        public TreeNode WaitForChildOfWorkspace(params string[] path) {
            return WaitForChildOfWorkspace(TimeSpan.FromSeconds(10), path);
        }

        public TreeNode WaitForChildOfWorkspace(TimeSpan timeout, params string[] path) {
            var item = WaitForItemHelper(p => FindChildOfWorkspaceHelper(p, false), path, timeout);
            // Check one more time, but now let the assertions be raised.
            return new TreeNode(FindChildOfWorkspaceHelper(path, true));
        }

        private AutomationElement FindChildOfWorkspaceHelper(string[] path, bool assertOnFailure) {
            var projElement = Nodes.FirstOrDefault()?.Element;
            if (assertOnFailure) {
                AutomationWrapper.DumpElement(Element);
                Assert.IsNotNull(projElement, "Did not find solution explorer workspace root element");
            }

            if (projElement == null) {
                return null;
            }

            var itemElement = path.Any() ? FindNode(
                projElement.FindAll(TreeScope.Children, Condition.TrueCondition),
                path,
                0
            ) : projElement;

            if (assertOnFailure) {
                AutomationWrapper.DumpElement(Element);
                Assert.IsNotNull(itemElement, string.Format("Did not find element <{0}>", string.Join("\\", path)));
            }
            return itemElement;
        }

        private AutomationElement FindChildOfProjectHelper(EnvDTE.Project project, string[] path, bool assertOnFailure) {
            var sln = project.DTE.Solution;
            int count = sln.Projects.OfType<EnvDTE.Project>().Count(p => {
                try {
                    return !string.IsNullOrEmpty(p.FullName);
                } catch (Exception) {
                    return false;
                }
            });
            var slnLabel = string.Format(
                "Solution '{0}' ",
                Path.GetFileNameWithoutExtension(sln.FullName)               
            );

            var slnElements = Element.FindAll(TreeScope.Children, new PropertyCondition(
                AutomationElement.NameProperty, slnLabel
            ));
            int slnCount = slnElements.OfType<AutomationElement>().Count();
            if (assertOnFailure) {
                Assert.AreEqual(1, slnCount, string.Format("Did not find single node <{0}>", slnLabel));
            } else if (slnCount != 1) {
                return null;
            }
            var slnElement = slnElements.Cast<AutomationElement>().Single();

            var projLabel = project.Name;
            var projElements = slnElement.FindAll(TreeScope.Children, new PropertyCondition(
                AutomationElement.NameProperty, projLabel
            ));
            int projCount = projElements.OfType<AutomationElement>().Count();
            if (assertOnFailure) {
                Assert.AreEqual(1, projCount, string.Format("Did not find single node <{0}>", projLabel));
            } else if (projCount != 1) {
                return null;
            }
            var projElement = projElements.Cast<AutomationElement>().Single();

            var itemElement = path.Any() ? FindNode(
                projElement.FindAll(TreeScope.Children, Condition.TrueCondition),
                path,
                0
            ) : projElement;

            if (assertOnFailure) {
                AutomationWrapper.DumpElement(Element);
                Assert.IsNotNull(itemElement, string.Format("Did not find element <{0}\\{1}\\{2}>", slnLabel, projLabel, string.Join("\\", path)));
            }
            return itemElement;
        }

    }
}
