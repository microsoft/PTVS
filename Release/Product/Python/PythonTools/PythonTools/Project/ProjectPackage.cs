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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project
{
    /// <summary>
    /// Defines abstract package.
    /// </summary>
    [ComVisible(true)]

    public abstract class ProjectPackage : Microsoft.VisualStudio.Shell.Package
    {
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
        protected internal IList<SolutionListener> SolutionListeners
        {
            get
            {
                return this.solutionListeners;
            }
        }
        #endregion

        #region methods
        protected override void Initialize()
        {
            base.Initialize();

            // Subscribe to the solution events
            this.solutionListeners.Add(new SolutionListenerForProjectOpen(this));
            this.solutionListeners.Add(new SolutionListenerForBuildDependencyUpdate(this));
            this.solutionListeners.Add(new SolutionListenerForProjectEvents(this));

            foreach (SolutionListener solutionListener in this.solutionListeners)
            {
                solutionListener.Init();
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Unadvise solution listeners.
            try
            {
                if (disposing)
                {
                    foreach (SolutionListener solutionListener in this.solutionListeners)
                    {
                        solutionListener.Dispose();
                    }
                }
            }
            finally
            {

                base.Dispose(disposing);
            }
        }
        #endregion
    }
}
