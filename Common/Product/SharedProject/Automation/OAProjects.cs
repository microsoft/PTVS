// Visual Studio Shared Project
// Copyright(c) DEVSENSE
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

namespace Microsoft.VisualStudioTools.Project.Automation
{

    /// <summary>
    /// Represents all of the projects of a given kind.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [ComVisible(true)]
    public class OAProjects : EnvDTE.Projects, IEnumerable<OAProject>
    {
        private readonly ProjectNode/*!*/project;

        #region ctor
        internal OAProjects(ProjectNode/*!*/project)
        {
            Utilities.ArgumentNotNull("project", project);
            this.project = project;
        }
        #endregion

        #region Projects Members

        /// <summary>
        /// Gets a value indicating the number of objects in the Projects collection.
        /// </summary>
        public int Count
        {
            get { return this.Enumerable.Count(); }
        }

        /// <summary>
        /// Gets the top-level extensibility object.
        /// </summary>
        public DTE DTE
        {
            get { return (EnvDTE.DTE)this.project.Site.GetService(typeof(EnvDTE.DTE)); ; }
        }

        /// <summary>
        /// Gets an enumerator for items in the collection.
        /// </summary>
        /// <returns>Enumerator for items in the collection.</returns>
        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)this.Enumerable;
        }

        /// <summary>
        /// Returns an indexed member of a Projects collection.
        /// </summary>
        /// <param name="index">Either index or a name of project to get.</param>
        /// <returns>Project reference.</returns>
        /// <exception cref="ArgumentException"><paramref name="index"/> does not correspond to an object in the collection.</exception>
        public EnvDTE.Project Item(object index)
        {
            EnvDTE.Project result = null;

            if (index is int)
            {
                result = this.Enumerable.ElementAt((int)index);
            }
            else if (index is string)
            {
                result = this.Enumerable.FirstOrDefault(x => x.Name == (string)index);
            }

            //
            if (result == null)
                throw new ArgumentException("index");

            return result;
        }

        /// <summary>
        /// Gets a GUID String indicating the kind or type of the object.
        /// </summary>
        public string Kind
        {
            get { return this.ProjectKind.ToString("B"); }
        }

        /// <summary>
        /// Gets the immediate parent object of a Projects collection.
        /// </summary>
        public DTE Parent
        {
            get { return this.DTE; }
        }

        public Properties Properties
        {
            get { throw new NotSupportedException(); }
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Kind of projects in this collection.
        /// </summary>
        private Guid ProjectKind { get { return this.project.ProjectGuid; } }

        private IEnumerable<OAProject> Enumerable { get { return (IEnumerable<OAProject>)this; } }

        #endregion

        #region IEnumerable<OAProject> Members

        /// <summary>
        /// Enumerates projects with the same <see cref="Kind"/>.
        /// </summary>
        /// <returns>Enumeration of <see cref="OAProject"/> of the same <see cref="Kind"/>.</returns>
        IEnumerator<OAProject> IEnumerable<OAProject>.GetEnumerator()
        {
            var/*!*/solution = this.project.Site.GetService(typeof(IVsSolution)) as IVsSolution;

            Guid kind = this.ProjectKind;
            IEnumHierarchies ppenum;

            // Enumerate all *loaded* projects,
            // projects being loaded will be added once OnAfterOpenProject is fired
            if (solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref kind, out ppenum) == VSConstants.S_OK && ppenum != null)
            {
                // enum project nodes, and its VSHPROPID_ProjectDir property:
                IVsHierarchy[] rgelt = new IVsHierarchy[16];
                uint pceltFetched;
                while (ppenum.Next((uint)rgelt.Length, rgelt, out pceltFetched) >= 0 && pceltFetched > 0)
                {
                    for (int i = 0; i < pceltFetched; i++)
                    {
                        var proj = rgelt[i].GetProject() as OAProject;
                        if (proj != null && proj.Project is ProjectNode && ((ProjectNode)proj.Project).ProjectGuid == kind)
                            yield return proj;
                    }
                }
            }
        }

        #endregion
    }
}
