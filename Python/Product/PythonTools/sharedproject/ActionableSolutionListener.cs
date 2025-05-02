//------------------------------------------------------------------------------
// <copyright file="ActionableSolutionListener.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;


namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Helper to listen to all the solution listener interfaces
    /// </summary>
    internal sealed class ActionableSolutionListener : SolutionListener {
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnBeforeCloseProjectEventHandler(IVsHierarchy hierarchy, int removed);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterLoadProjectEventHandler(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterRenameProjectEventHandler(IVsHierarchy hierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnBeforeUnloadProjectEventHandler(IVsHierarchy realHierarchy, IVsHierarchy stubHierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterAsynchOpenProjectEventHandler(IVsHierarchy hierarchy, int added);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterChangeProjectParentEventHandler(IVsHierarchy hierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterCloseSolutionEventHandler(object reserved);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterClosingChildrenEventHandler(IVsHierarchy hierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterMergeSolutionEventHandler(object unknownReserved);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterOpeningChildrenEventHandler(IVsHierarchy hierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterOpenProjectEventHandler(IVsHierarchy hierarchy, int added);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnAfterOpenSolutionEventHandler(object unknownReserved, int newSolution);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnBeforeCloseSolutionEventHandler(object unknownReserved);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnBeforeClosingChildrenEventHandler(IVsHierarchy hierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnBeforeOpeningChildrenEventHandler(IVsHierarchy hierarchy);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnQueryChangeProjectParentEventHandler(IVsHierarchy hierarchy, IVsHierarchy newParentHierarchy, ref int cancel);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnQueryCloseProjectEventHandler(IVsHierarchy hierarchy, int removing, ref int cancel);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnQueryCloseSolutionEventHandler(object unknownReserved, ref int cancel);

        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
        public delegate int OnQueryUnloadProjectEventHandler(IVsHierarchy realHierarchy, ref int cancel);

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnBeforeCloseProjectEventHandler BeforeCloseProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterLoadProjectEventHandler AfterLoadProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterRenameProjectEventHandler AfterRenameProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnBeforeUnloadProjectEventHandler BeforeUnloadProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Asynch")]
        public event OnAfterAsynchOpenProjectEventHandler AfterAsynchOpenProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterChangeProjectParentEventHandler AfterChangeProjectParentEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterCloseSolutionEventHandler AfterCloseSolutionEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterClosingChildrenEventHandler AfterClosingChildrenEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterMergeSolutionEventHandler AfterMergeSolutionEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterOpeningChildrenEventHandler AfterOpeningChildrenEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterOpenProjectEventHandler AfterOpenProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnAfterOpenSolutionEventHandler AfterOpenSolutionEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnBeforeCloseSolutionEventHandler BeforeCloseSolutionEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnBeforeClosingChildrenEventHandler BeforeClosingChildrenEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1713:EventsShouldNotHaveBeforeOrAfterPrefix")]
        public event OnBeforeOpeningChildrenEventHandler BeforeOpeningChildrenEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        public event OnQueryChangeProjectParentEventHandler QueryChangeProjectParentEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        public event OnQueryCloseProjectEventHandler QueryCloseProjectEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        public event OnQueryCloseSolutionEventHandler QueryCloseSolutionEvent;

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        public event OnQueryUnloadProjectEventHandler QueryUnloadProjectEvent;

        #region ctor
        public ActionableSolutionListener(IServiceProvider serviceProvider)
            : base(serviceProvider) {
        }
        #endregion

        public override int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed) {
            if (BeforeCloseProjectEvent != null) {
                return BeforeCloseProjectEvent(hierarchy, removed);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterLoadProject(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy) {
            if (AfterLoadProjectEvent != null) {
                return AfterLoadProjectEvent(stubHierarchy, realHierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterRenameProject(IVsHierarchy hierarchy) {
            if (AfterRenameProjectEvent != null) {
                return AfterRenameProjectEvent(hierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnBeforeUnloadProject(IVsHierarchy realHierarchy, IVsHierarchy stubHierarchy) {
            if (BeforeUnloadProjectEvent != null) {
                return BeforeUnloadProjectEvent(realHierarchy, stubHierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterAsynchOpenProject(IVsHierarchy hierarchy, int added) {
            if (AfterAsynchOpenProjectEvent != null) {
                return AfterAsynchOpenProjectEvent(hierarchy, added);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterChangeProjectParent(IVsHierarchy hierarchy) {
            if (AfterChangeProjectParentEvent != null) {
                return AfterChangeProjectParentEvent(hierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterCloseSolution(object reserved) {
            if (AfterCloseSolutionEvent != null) {
                return AfterCloseSolutionEvent(reserved);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterClosingChildren(IVsHierarchy hierarchy) {
            if (AfterClosingChildrenEvent != null) {
                return AfterClosingChildrenEvent(hierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterMergeSolution(object unknownReserved) {
            if (AfterMergeSolutionEvent != null) {
                return AfterMergeSolutionEvent(unknownReserved);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterOpeningChildren(IVsHierarchy hierarchy) {
            if (AfterOpeningChildrenEvent != null) {
                return AfterOpeningChildrenEvent(hierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterOpenProject(IVsHierarchy hierarchy, int added) {
            if (AfterOpenProjectEvent != null) {
                return AfterOpenProjectEvent(hierarchy, added);
            }
            return VSConstants.S_OK;
        }
        public override int OnAfterOpenSolution(object unknownReserved, int newSolution) {
            if (AfterOpenSolutionEvent != null) {
                return AfterOpenSolutionEvent(unknownReserved, newSolution);
            }
            return VSConstants.S_OK;
        }
        public override int OnBeforeCloseSolution(object unknownReserved) {
            if (BeforeCloseSolutionEvent != null) {
                return BeforeCloseSolutionEvent(unknownReserved);
            }
            return VSConstants.S_OK;
        }
        public override int OnBeforeClosingChildren(IVsHierarchy hierarchy) {
            if (BeforeClosingChildrenEvent != null) {
                return BeforeClosingChildrenEvent(hierarchy);
            }
            return VSConstants.S_OK;
        }
        public override int OnBeforeOpeningChildren(IVsHierarchy hierarchy) {
            if (BeforeOpeningChildrenEvent != null) {
                return BeforeOpeningChildrenEvent(hierarchy);
            }
            return VSConstants.S_OK;
        }
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "2#")]
        public override int OnQueryChangeProjectParent(IVsHierarchy hierarchy, IVsHierarchy newParentHierarchy, ref int cancel) {
            if (QueryChangeProjectParentEvent != null) {
                return QueryChangeProjectParentEvent(hierarchy, newParentHierarchy, ref cancel);
            }
            return VSConstants.S_OK;
        }
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "2#")]
        public override int OnQueryCloseProject(IVsHierarchy hierarchy, int removing, ref int cancel) {
            if (QueryCloseProjectEvent != null) {
                return QueryCloseProjectEvent(hierarchy, removing, ref cancel);
            }
            return VSConstants.S_OK;
        }
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "1#")]
        public override int OnQueryCloseSolution(object unknownReserved, ref int cancel) {
            if (QueryCloseSolutionEvent != null) {
                return QueryCloseSolutionEvent(unknownReserved, ref cancel);
            }
            return VSConstants.S_OK;
        }
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "1#")]
        public override int OnQueryUnloadProject(IVsHierarchy realHierarchy, ref int cancel) {
            if (QueryUnloadProjectEvent != null) {
                return QueryUnloadProjectEvent(realHierarchy, ref cancel);
            }
            return VSConstants.S_OK;
        }
    }
}
