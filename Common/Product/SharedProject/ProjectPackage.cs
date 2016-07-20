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

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Defines abstract package.
    /// </summary>
    [ComVisible(true)]

    public abstract class ProjectPackage : Microsoft.VisualStudio.Shell.Package {
        #region fields
        /// <summary>
        /// This is the place to register all the solution listeners.
        /// </summary>
        private List<SolutionListener> solutionListeners = new List<SolutionListener>();
        #endregion

        #region properties
        /// <summary>
        /// Add your listener to this list. They should be added in the overridden Initialize befaore calling the base.
        /// </summary>
        internal IList<SolutionListener> SolutionListeners {
            get {
                return this.solutionListeners;
            }
        }
        #endregion

        #region methods
        protected override void Initialize() {
            base.Initialize();

            // Subscribe to the solution events
            this.solutionListeners.Add(new SolutionListenerForProjectOpen(this));
            this.solutionListeners.Add(new SolutionListenerForBuildDependencyUpdate(this));

            foreach (SolutionListener solutionListener in this.solutionListeners) {
                solutionListener.Init();
            }
        }

        protected override void Dispose(bool disposing) {
            // Unadvise solution listeners.
            try {
                if (disposing) {
                    foreach (SolutionListener solutionListener in this.solutionListeners) {
                        solutionListener.Dispose();
                    }
                }
            } finally {

                base.Dispose(disposing);
            }
        }
        #endregion
    }
}
