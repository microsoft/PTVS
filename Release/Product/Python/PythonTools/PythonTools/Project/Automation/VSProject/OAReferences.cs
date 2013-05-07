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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;
using VSLangProj;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;

namespace Microsoft.VisualStudioTools.Project.Automation
{
    /// <summary>
    /// Represents the automation object for the equivalent ReferenceContainerNode object
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    [ComVisible(true)]
    public class OAReferences : ConnectionPointContainer,
                                IEventSource<_dispReferencesEvents>,
                                References,
                                ReferencesEvents
    {
        private ReferenceContainerNode container;
        internal OAReferences(ReferenceContainerNode containerNode)
        {
            container = containerNode;
            AddEventSource<_dispReferencesEvents>(this as IEventSource<_dispReferencesEvents>);
            container.OnChildAdded += new EventHandler<HierarchyNodeEventArgs>(OnReferenceAdded);
            container.OnChildRemoved += new EventHandler<HierarchyNodeEventArgs>(OnReferenceRemoved);
        }

        #region Private Members
        private Reference AddFromSelectorData(VSCOMPONENTSELECTORDATA selector)
        {
            ReferenceNode refNode = container.AddReferenceFromSelectorData(selector);
            if (null == refNode)
            {
                return null;
            }

            return refNode.Object as Reference;
        }

        private Reference FindByName(string stringIndex)
        {
            foreach (Reference refNode in this)
            {
                if (0 == string.Compare(refNode.Name, stringIndex, StringComparison.Ordinal))
                {
                    return refNode;
                }
            }
            return null;
        }
        #endregion

        #region References Members

        public Reference Add(string bstrPath)
        {
            // ignore requests from the designer which are framework assemblies and start w/ a *.
            if (string.IsNullOrEmpty(bstrPath) || bstrPath.StartsWith("*"))
            {
                return null;
            }
            VSCOMPONENTSELECTORDATA selector = new VSCOMPONENTSELECTORDATA();
            selector.type = VSCOMPONENTTYPE.VSCOMPONENTTYPE_File;
            selector.bstrFile = bstrPath;

            return AddFromSelectorData(selector);
        }

        public Reference AddActiveX(string bstrTypeLibGuid, int lMajorVer, int lMinorVer, int lLocaleId, string bstrWrapperTool)
        {
            VSCOMPONENTSELECTORDATA selector = new VSCOMPONENTSELECTORDATA();
            selector.type = VSCOMPONENTTYPE.VSCOMPONENTTYPE_Com2;
            selector.guidTypeLibrary = new Guid(bstrTypeLibGuid);
            selector.lcidTypeLibrary = (uint)lLocaleId;
            selector.wTypeLibraryMajorVersion = (ushort)lMajorVer;
            selector.wTypeLibraryMinorVersion = (ushort)lMinorVer;

            return AddFromSelectorData(selector);
        }

        public Reference AddProject(EnvDTE.Project project)
        {
            if (null == project)
            {
                return null;
            }
            // Get the soulution.
            IVsSolution solution = container.ProjectMgr.Site.GetService(typeof(SVsSolution)) as IVsSolution;
            if (null == solution)
            {
                return null;
            }

            // Get the hierarchy for this project.
            IVsHierarchy projectHierarchy;
            ErrorHandler.ThrowOnFailure(solution.GetProjectOfUniqueName(project.UniqueName, out projectHierarchy));

            // Create the selector data.
            VSCOMPONENTSELECTORDATA selector = new VSCOMPONENTSELECTORDATA();
            selector.type = VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project;

            // Get the project reference string.
            ErrorHandler.ThrowOnFailure(solution.GetProjrefOfProject(projectHierarchy, out selector.bstrProjRef));

            selector.bstrTitle = project.Name;
            selector.bstrFile = System.IO.Path.GetDirectoryName(project.FullName);

            return AddFromSelectorData(selector);
        }

        public EnvDTE.Project ContainingProject
        {
            get
            {
                return container.ProjectMgr.GetAutomationObject() as EnvDTE.Project;
            }
        }

        public int Count
        {
            get
            {
                return container.EnumReferences().Count;
            }
        }

        public EnvDTE.DTE DTE
        {
            get
            {
                return container.ProjectMgr.Site.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            }
        }

        public Reference Find(string bstrIdentity)
        {
            if (string.IsNullOrEmpty(bstrIdentity))
            {
                return null;
            }
            foreach (Reference refNode in this)
            {
                if (null != refNode)
                {
                    if (0 == string.Compare(bstrIdentity, refNode.Identity, StringComparison.Ordinal))
                    {
                        return refNode;
                    }
                }
            }
            return null;
        }

        public IEnumerator GetEnumerator()
        {
            List<Reference> references = new List<Reference>();
            IEnumerator baseEnum = container.EnumReferences().GetEnumerator();
            if (null == baseEnum)
            {
                return references.GetEnumerator();
            }
            while (baseEnum.MoveNext())
            {
                ReferenceNode refNode = baseEnum.Current as ReferenceNode;
                if (null == refNode)
                {
                    continue;
                }
                Reference reference = refNode.Object as Reference;
                if (null != reference)
                {
                    references.Add(reference);
                }
            }
            return references.GetEnumerator();
        }

        public Reference Item(object index)
        {
            string stringIndex = index as string;
            if (null != stringIndex)
            {
                return FindByName(stringIndex);
            }
            // Note that this cast will throw if the index is not convertible to int.
            int intIndex = (int)index;
            IList<ReferenceNode> refs = container.EnumReferences();
            if (null == refs)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            if ((intIndex <= 0) || (intIndex > refs.Count))
            {
                throw new ArgumentOutOfRangeException("index");
            }
            // Let the implementation of IList<> throw in case of index not correct.
            return refs[intIndex - 1].Object as Reference;
        }

        public object Parent
        {
            get
            {
                return container.Parent.Object;
            }
        }

        #endregion

        #region _dispReferencesEvents_Event Members
        public event _dispReferencesEvents_ReferenceAddedEventHandler ReferenceAdded;
        public event _dispReferencesEvents_ReferenceChangedEventHandler ReferenceChanged;
        public event _dispReferencesEvents_ReferenceRemovedEventHandler ReferenceRemoved;
        #endregion

        #region Callbacks for the HierarchyNode events
        private void OnReferenceAdded(object sender, HierarchyNodeEventArgs args)
        {
            // Validate the parameters.
            if ((container != sender as ReferenceContainerNode) ||
                (null == args) || (null == args.Child))
            {
                return;
            }

            // Check if there is any sink for this event.
            if (null == ReferenceAdded)
            {
                return;
            }

            // Check that the removed item implements the Reference interface.
            Reference reference = args.Child.Object as Reference;
            if (null != reference)
            {
                ReferenceAdded(reference);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode",
            Justification = "Support for this has not yet been added")]
        private void OnReferenceChanged(object sender, HierarchyNodeEventArgs args)
        {
            // Validate the parameters.
            if ((container != sender as ReferenceContainerNode) ||
                (null == args) || (null == args.Child))
            {
                return;
            }

            // Check if there is any sink for this event.
            if (null == ReferenceChanged)
            {
                return;
            }

            // Check that the removed item implements the Reference interface.
            Reference reference = args.Child.Object as Reference;
            if (null != reference)
            {
                ReferenceChanged(reference);
            }
        }

        private void OnReferenceRemoved(object sender, HierarchyNodeEventArgs args)
        {
            // Validate the parameters.
            if ((container != sender as ReferenceContainerNode) ||
                (null == args) || (null == args.Child))
            {
                return;
            }

            // Check if there is any sink for this event.
            if (null == ReferenceRemoved)
            {
                return;
            }

            // Check that the removed item implements the Reference interface.
            Reference reference = args.Child.Object as Reference;
            if (null != reference)
            {
                ReferenceRemoved(reference);
            }
        }
        #endregion

        #region IEventSource<_dispReferencesEvents> Members
        void IEventSource<_dispReferencesEvents>.OnSinkAdded(_dispReferencesEvents sink)
        {
            ReferenceAdded += new _dispReferencesEvents_ReferenceAddedEventHandler(sink.ReferenceAdded);
            ReferenceChanged += new _dispReferencesEvents_ReferenceChangedEventHandler(sink.ReferenceChanged);
            ReferenceRemoved += new _dispReferencesEvents_ReferenceRemovedEventHandler(sink.ReferenceRemoved);
        }

        void IEventSource<_dispReferencesEvents>.OnSinkRemoved(_dispReferencesEvents sink)
        {
            ReferenceAdded -= new _dispReferencesEvents_ReferenceAddedEventHandler(sink.ReferenceAdded);
            ReferenceChanged -= new _dispReferencesEvents_ReferenceChangedEventHandler(sink.ReferenceChanged);
            ReferenceRemoved -= new _dispReferencesEvents_ReferenceRemovedEventHandler(sink.ReferenceRemoved);
        }
        #endregion
    }
}
