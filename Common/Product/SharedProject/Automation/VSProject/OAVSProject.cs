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

namespace Microsoft.VisualStudioTools.Project.Automation
{
    /// <summary>
    /// Represents an automation friendly version of a language-specific project.
    /// </summary>
    [ComVisible(true), CLSCompliant(false)]
    public class OAVSProject : VSProject
    {
        #region fields
        private ProjectNode project;
        private OAVSProjectEvents events;
        #endregion

        #region ctors
        internal OAVSProject(ProjectNode project)
        {
            this.project = project;
        }
        #endregion

        #region VSProject Members

        public virtual ProjectItem AddWebReference(string bstrUrl)
        {
            throw new NotImplementedException();
        }

        public virtual BuildManager BuildManager
        {
            get
            {
                return null;
            }
        }

        public virtual void CopyProject(string bstrDestFolder, string bstrDestUNCPath, prjCopyProjectOption copyProjectOption, string bstrUsername, string bstrPassword)
        {
            throw new NotImplementedException();
        }

        public virtual ProjectItem CreateWebReferencesFolder()
        {
            throw new NotImplementedException();
        }

        public virtual DTE DTE
        {
            get
            {
                return (EnvDTE.DTE)this.project.Site.GetService(typeof(EnvDTE.DTE));
            }
        }

        public virtual VSProjectEvents Events
        {
            get
            {
                if (events == null)
                    events = new OAVSProjectEvents(this);
                return events;
            }
        }

        public virtual void Exec(prjExecCommand command, int bSuppressUI, object varIn, out object pVarOut)
        {
            throw new NotImplementedException(); ;
        }

        public virtual void GenerateKeyPairFiles(string strPublicPrivateFile, string strPublicOnlyFile)
        {
            throw new NotImplementedException(); ;
        }

        public virtual string GetUniqueFilename(object pDispatch, string bstrRoot, string bstrDesiredExt)
        {
            throw new NotImplementedException(); ;
        }

        public virtual Imports Imports
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public virtual EnvDTE.Project Project
        {
            get
            {
                return this.project.GetAutomationObject() as EnvDTE.Project;
            }
        }

        public virtual References References
        {
            get
            {
                ReferenceContainerNode references = project.GetReferenceContainer() as ReferenceContainerNode;
                if (null == references)
                {
                    return new OAReferences(null, project);
                }
                return references.Object as References;
            }
        }

        public virtual void Refresh()
        {
        }

        public virtual string TemplatePath
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public virtual ProjectItem WebReferencesFolder
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public virtual bool WorkOffline
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }

    /// <summary>
    /// Provides access to language-specific project events
    /// </summary>
    [ComVisible(true), CLSCompliant(false)]
    public class OAVSProjectEvents : VSProjectEvents
    {
        #region fields
        private OAVSProject vsProject;
        #endregion

        #region ctors
        public OAVSProjectEvents(OAVSProject vsProject)
        {
            this.vsProject = vsProject;
        }
        #endregion

        #region VSProjectEvents Members

        public virtual BuildManagerEvents BuildManagerEvents
        {
            get
            {
                return vsProject.BuildManager as BuildManagerEvents;
            }
        }

        public virtual ImportsEvents ImportsEvents
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public virtual ReferencesEvents ReferencesEvents
        {
            get
            {
                return vsProject.References as ReferencesEvents;
            }
        }

        #endregion
    }

}
