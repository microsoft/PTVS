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
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.VisualStudioTools.Project
{
    /// <summary>
    /// Creates projects within the solution
    /// </summary>

    public abstract class ProjectFactory : Microsoft.VisualStudio.Shell.Flavor.FlavoredProjectFactoryBase
#if DEV11_OR_LATER
        , IVsAsynchronousProjectCreate
#endif
    {
        #region fields
        private Microsoft.VisualStudio.Shell.Package package;
        private System.IServiceProvider site;

        /// <summary>
        /// The msbuild engine that we are going to use.
        /// </summary>
        private MSBuild.ProjectCollection buildEngine;

        /// <summary>
        /// The msbuild project for the project file.
        /// </summary>
        private MSBuild.Project buildProject;
#if DEV11_OR_LATER
        private static readonly Lazy<IVsTaskSchedulerService> taskSchedulerService = new Lazy<IVsTaskSchedulerService>(() => Package.GetGlobalService(typeof(SVsTaskSchedulerService)) as IVsTaskSchedulerService);
#endif
        #endregion

        #region properties
        protected Microsoft.VisualStudio.Shell.Package Package
        {
            get
            {
                return this.package;
            }
        }

        protected System.IServiceProvider Site
        {
            get
            {
                return this.site;
            }
        }

        #endregion

        #region ctor
        protected ProjectFactory(Microsoft.VisualStudio.Shell.Package package)
        {
            this.package = package;
            this.site = package;

            // Please be aware that this methods needs that ServiceProvider is valid, thus the ordering of calls in the ctor matters.
            this.buildEngine = Utilities.InitializeMsBuildEngine(this.buildEngine, this.site);
        }
        #endregion

        #region abstract methods
        internal abstract ProjectNode CreateProject();
        #endregion

        #region overriden methods
        /// <summary>
        /// Rather than directly creating the project, ask VS to initate the process of
        /// creating an aggregated project in case we are flavored. We will be called
        /// on the IVsAggregatableProjectFactory to do the real project creation.
        /// </summary>
        /// <param name="fileName">Project file</param>
        /// <param name="location">Path of the project</param>
        /// <param name="name">Project Name</param>
        /// <param name="flags">Creation flags</param>
        /// <param name="projectGuid">Guid of the project</param>
        /// <param name="project">Project that end up being created by this method</param>
        /// <param name="canceled">Was the project creation canceled</param>
        protected override void CreateProject(string fileName, string location, string name, uint flags, ref Guid projectGuid, out IntPtr project, out int canceled)
        {
            using (new DebugTimer("CreateProject"))
            {
                project = IntPtr.Zero;
                canceled = 0;

                // Get the list of GUIDs from the project/template
                string guidsList = this.ProjectTypeGuids(fileName);

                // Launch the aggregate creation process (we should be called back on our IVsAggregatableProjectFactoryCorrected implementation)
                IVsCreateAggregateProject aggregateProjectFactory = (IVsCreateAggregateProject)this.Site.GetService(typeof(SVsCreateAggregateProject));
                int hr = aggregateProjectFactory.CreateAggregateProject(guidsList, fileName, location, name, flags, ref projectGuid, out project);
                if (hr == VSConstants.E_ABORT)
                    canceled = 1;
                ErrorHandler.ThrowOnFailure(hr);

                this.buildProject = null;
            }
        }

        /// <summary>
        /// Instantiate the project class, but do not proceed with the
        /// initialization just yet.
        /// Delegate to CreateProject implemented by the derived class.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope",
            Justification = "The global property handles is instantiated here and used in the project node that will Dispose it")]
        protected override object PreCreateForOuter(IntPtr outerProjectIUnknown)
        {
            Utilities.CheckNotNull(this.buildProject, "The build project should have been initialized before calling PreCreateForOuter.");

            // Please be very carefull what is initialized here on the ProjectNode. Normally this should only instantiate and return a project node.
            // The reason why one should very carefully add state to the project node here is that at this point the aggregation has not yet been created and anything that would cause a CCW for the project to be created would cause the aggregation to fail
            // Our reasoning is that there is no other place where state on the project node can be set that is known by the Factory and has to execute before the Load method.
            ProjectNode node = this.CreateProject();
            Utilities.CheckNotNull(node, "The project failed to be created");
            node.BuildEngine = this.buildEngine;
            node.BuildProject = this.buildProject;
            node.Package = this.package as ProjectPackage;
            return node;
        }

        /// <summary>
        /// Retrives the list of project guids from the project file.
        /// If you don't want your project to be flavorable, override
        /// to only return your project factory Guid:
        ///      return this.GetType().GUID.ToString("B");
        /// </summary>
        /// <param name="file">Project file to look into to find the Guid list</param>
        /// <returns>List of semi-colon separated GUIDs</returns>
        protected override string ProjectTypeGuids(string file)
        {
            // Load the project so we can extract the list of GUIDs

            this.buildProject = Utilities.ReinitializeMsBuildProject(this.buildEngine, file, this.buildProject);

            // Retrieve the list of GUIDs, if it is not specify, make it our GUID
            string guids = buildProject.GetPropertyValue(ProjectFileConstants.ProjectTypeGuids);
            if (String.IsNullOrEmpty(guids))
                guids = this.GetType().GUID.ToString("B");

            return guids;
        }
        #endregion

#if DEV11_OR_LATER

        public virtual bool CanCreateProjectAsynchronously(ref Guid rguidProjectID, string filename, uint flags)
        {
            return true;
        }

        public void OnBeforeCreateProjectAsync(ref Guid rguidProjectID, string filename, string location, string pszName, uint flags)
        {
        }

        public IVsTask CreateProjectAsync(ref Guid rguidProjectID, string filename, string location, string pszName, uint flags)
        {
            Guid iid = typeof(IVsHierarchy).GUID;
            return VsTaskLibraryHelper.CreateAndStartTask(taskSchedulerService.Value, VsTaskRunContext.UIThreadBackgroundPriority, VsTaskLibraryHelper.CreateTaskBody(() =>
            {
                IntPtr project;
                int cancelled;
                CreateProject(filename, location, pszName, flags, ref iid, out project, out cancelled);
                if (cancelled != 0)
                {
                    throw new OperationCanceledException();
                }

                return Marshal.GetObjectForIUnknown(project);
            }));
        }

#endif

    }
}
