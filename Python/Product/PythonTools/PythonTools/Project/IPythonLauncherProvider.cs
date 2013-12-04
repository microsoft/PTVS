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


using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    public interface IPythonLauncherProvider {

        /// <summary>
        /// Gets the options for the provided launcher.
        /// </summary>
        /// <returns></returns>
        IPythonLauncherOptions GetLauncherOptions(IPythonProject properties);

        /// <summary>
        /// Gets the canonical name of the launcher.
        /// </summary>
        /// <remarks>
        /// This name is used to reference the launcher from project files and
        /// should not vary with language or culture. To specify a culture-
        /// sensitive name, use
        /// <see cref="IPythonLauncherProvider2.LocalizedName"/>.
        /// </remarks>
        string Name {
            get;
        }

        /// <summary>
        /// Gets a longer description of the launcher.
        /// </summary>
        string Description {
            get;
        }

        IProjectLauncher CreateLauncher(IPythonProject project);
    }

    public interface IPythonLauncherProvider2 : IPythonLauncherProvider {
        /// <summary>
        /// Gets the localized name of the launcher.
        /// </summary>
        string LocalizedName {
            get;
        }

        /// <summary>
        /// Gets the sort priority of the launcher. Lower values sort earlier in
        /// user-visible lists.
        /// </summary>
        int SortPriority {
            get;
        }
    }
}
