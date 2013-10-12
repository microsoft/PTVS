using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;

namespace Microsoft.VisualStudioTools.Project
{
    /// <summary>
    /// Helper class which fires IVsExtensibility3 events if not in suspended state.
    /// </summary>
    internal sealed class ExtensibilityEventsDispatcher
    {
        private class SuspendLock : IDisposable
        {
            private readonly bool previousState;
            private readonly ExtensibilityEventsDispatcher owner;

            public SuspendLock(ExtensibilityEventsDispatcher owner)
            {
                this.owner = owner;
                this.previousState = this.owner.suspended;
                this.owner.suspended = true;
            }

            void IDisposable.Dispose()
            {
                this.owner.suspended = this.previousState;
            }
        }

        private readonly ProjectNode project;
        private bool suspended;

        public ExtensibilityEventsDispatcher(ProjectNode/*!*/ project)
        {
            Utilities.ArgumentNotNull("project", project);

            this.project = project;
        }

        public IDisposable Suspend()
        {
            return new ExtensibilityEventsDispatcher.SuspendLock(this);
        }

        public void FireItemAdded(HierarchyNode node)
        {
            this.Fire(node, (IVsExtensibility3 vsExtensibility, ProjectItem pi)=>
            {
                vsExtensibility.FireProjectItemsEvent_ItemAdded(pi);
            });
        }

        public void FireItemRemoved(HierarchyNode node)
        {
            this.Fire(node, (IVsExtensibility3 vsExtensibility, ProjectItem pi)=>
            {
                vsExtensibility.FireProjectItemsEvent_ItemRemoved(pi);
            });
        }

        public void FireItemRenamed(HierarchyNode node, string oldName)
        {
            this.Fire(node, (IVsExtensibility3 vsExtensibility, ProjectItem pi)=>
            {
                vsExtensibility.FireProjectItemsEvent_ItemRenamed(pi, oldName);
            });
        }

        private void Fire(HierarchyNode node, Action<IVsExtensibility3, ProjectItem> fire)
        {
            // When we are in suspended mode. Do not fire anything
            if (this.suspended)
            {
                return;
            }

            // Project has to be opened
            if (!this.project.IsProjectOpened)
            {
                return;
            }

            // We don't want to fire events for references here. OAReferences should do the job
            if (node is ReferenceNode)
            {
                return;
            }

            IVsExtensibility3 vsExtensibility = this.project.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;
            if (vsExtensibility != null)
            {
                object obj = node.GetAutomationObject();
                ProjectItem item = obj as ProjectItem;
                if (item != null)
                {
                    fire(vsExtensibility, item);
                }
            }
        }
    }
}
