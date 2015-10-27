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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// This is a helper class which fires IVsExtensibility3 events if not in suspended state.
    /// </summary>
    internal sealed class ExtensibilityEventsDispatcher {
        private class SuspendLock : IDisposable {
            private readonly bool _previousState;
            private readonly ExtensibilityEventsDispatcher _owner;

            public SuspendLock(ExtensibilityEventsDispatcher owner) {
                this._owner = owner;
                this._previousState = this._owner._suspended;
                this._owner._suspended = true;
            }

            void IDisposable.Dispose() {
                this._owner._suspended = this._previousState;
            }
        }

        private readonly ProjectNode _project;
        private bool _suspended;

        public ExtensibilityEventsDispatcher(ProjectNode/*!*/ project) {
            Utilities.ArgumentNotNull("project", project);

            this._project = project;
        }

        /// <summary>
        /// Creates a lock which suspends firing of the events until it gets disposed.
        /// </summary>
        public IDisposable Suspend() {
            return new SuspendLock(this);
        }

        public void FireItemAdded(HierarchyNode node) {
            this.Fire(node, (IVsExtensibility3 vsExtensibility, ProjectItem item) => {
                vsExtensibility.FireProjectItemsEvent_ItemAdded(item);
            });
        }

        public void FireItemRemoved(HierarchyNode node) {
            this.Fire(node, (IVsExtensibility3 vsExtensibility, ProjectItem item) => {
                vsExtensibility.FireProjectItemsEvent_ItemRemoved(item);
            });
        }

        public void FireItemRenamed(HierarchyNode node, string oldName) {
            this.Fire(node, (IVsExtensibility3 vsExtensibility, ProjectItem item) => {
                vsExtensibility.FireProjectItemsEvent_ItemRenamed(item, oldName);
            });
        }

        private void Fire(HierarchyNode node, Action<IVsExtensibility3, ProjectItem> fire) {
            // When we are in suspended mode. Do not fire anything
            if (this._suspended) {
                return;
            }

            // Project has to be opened
            if (!this._project.IsProjectOpened) {
                return;
            }

            // We don't want to fire events for references here. OAReferences should do the job
            if (node is ReferenceNode) {
                return;
            }

            IVsExtensibility3 vsExtensibility = this._project.GetService(typeof(IVsExtensibility)) as IVsExtensibility3;
            if (vsExtensibility != null) {
                object obj = node.GetAutomationObject();
                ProjectItem item = obj as ProjectItem;
                if (item != null) {
                    fire(vsExtensibility, item);
                }
            }
        }
    }
}
