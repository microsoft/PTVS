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
using System.Linq;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.SharedProject;

namespace TestUtilities.UI {
    /// <summary>
    /// Wrapper around a generated SolutionFile.  Provides helpers for simplifying
    /// interacting with the solution loaded into Solution Explorer.
    /// </summary>
    public class VisualStudioSolution : IDisposable {
        private readonly SolutionFile _solution;
        private readonly VisualStudioApp _app;
        public readonly SolutionExplorerTree SolutionExplorer;
        public readonly EnvDTE.Project Project;
        private bool _disposed;

        public VisualStudioSolution(SolutionFile solution) {
            _solution = solution;
            _app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            Project = _app.OpenProject(solution.Filename);

            ThreadHelper.Generic.Invoke(Keyboard.Reset);
            SolutionExplorer = _app.OpenSolutionExplorer();
            SelectSolutionNode();
        }

        public VisualStudioApp App {
            get {
                return _app;
            }
        }

        /// <summary>
        /// Opens the specified filename from the specified project name.
        /// </summary>
        public EditorWindow OpenItem(string project, params string[] path) {
            foreach (EnvDTE.Project proj in VsIdeTestHostContext.Dte.Solution.Projects) {
                if (proj.Name == project) {
                    var items = proj.ProjectItems;
                    EnvDTE.ProjectItem item = null;
                    foreach (var itemName in path) {
                        item = items.Item(itemName);
                        items = item.ProjectItems;
                    }
                    Assert.IsNotNull(item);
                    var window = item.Open();
                    window.Activate();
                    return App.GetDocument(item.Document.FullName);
                    
                }
            }

            throw new InvalidOperationException(
                String.Format(
                    "Failed to find {0} item in project {1}",
                    String.Join("\\", path),
                    project
                )
            );
        }

        public AutomationElement FindItem(params string[] path) {
            return SolutionExplorer.FindItem(AddSolutionToPath(path));
        }

        private string[] AddSolutionToPath(string[] path) {
            return new[] { SolutionNodeText }.Concat(path).ToArray();
        }

        public AutomationElement WaitForItem(params string[] path) {
            return SolutionExplorer.WaitForItem(AddSolutionToPath(path));
        }

        public AutomationElement WaitForItemRemoved(params string[] path) {
            return SolutionExplorer.WaitForItemRemoved(AddSolutionToPath(path));
        }

        public string Filename {
            get {
                return _solution.Filename;
            }
        }

        public string Directory {
            get {
                return _solution.Directory;
            }
        }

        private string SolutionNodeText {
            get {
                if (_solution.Projects.Count(sln => !sln.Flags.HasFlag(SolutionElementFlags.ExcludeFromConfiguration) && !sln.Flags.HasFlag(SolutionElementFlags.ExcludeFromSolution)) > 1) {
                    return String.Format(
                        "Solution '{0}' ({1} projects)",
                        Path.GetFileNameWithoutExtension(_solution.Filename),
                        _solution.Projects.Length
                    );
                }
                return String.Format(
                    "Solution '{0}' (1 project)",
                    Path.GetFileNameWithoutExtension(_solution.Filename)
                );

            }
        }

        /// <summary>
        /// Selects the solution node using the mouse.
        /// 
        /// This is used to reset the state of the mouse before a test as some
        /// tests can cause the mouse to be left in an odd state - the mouse up
        /// event is delivered to solution explorer, but selecting items later
        /// doesn't work because the mouse is left in an odd state.  If you
        /// make this method a nop and try and run all of the tests you'll
        /// see the bad behavior.
        /// </summary>
        public void SelectSolutionNode() {
            var item = SolutionExplorer.WaitForItem(SolutionNodeText);
            SolutionExplorer.CenterInView(item);
            Mouse.MoveTo(item.GetClickablePoint());
            Mouse.Click(MouseButton.Left);
        }

        #region IDisposable Members

        ~VisualStudioSolution() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                if (disposing) {
                    _app.Dispose();
                    _solution.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion

        public void AssertFileExists(params string[] path) {
            SolutionExplorer.AssertFileExists(Directory, AddSolutionToPath(path));
        }

        public void AssertFileDoesntExist(params string[] path) {
            SolutionExplorer.AssertFileDoesntExist(Directory, AddSolutionToPath(path));
        }

        public void AssertFolderExists(params string[] path) {
            SolutionExplorer.AssertFolderExists(Directory, AddSolutionToPath(path));
        }

        public void AssertFolderDoesntExist(params string[] path) {
            SolutionExplorer.AssertFolderDoesntExist(Directory, AddSolutionToPath(path));
        }

        public void AssertFileExistsWithContent(string content, params string[] path) {
            SolutionExplorer.AssertFileExistsWithContent(Directory, content, AddSolutionToPath(path));
        }
    }
}
