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
using System.Windows.Forms;

namespace Microsoft.PythonTools.Project {
    public interface IPythonLauncherOptions {
        /// <summary>
        /// Saves the current launcher options to storage.
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Loads the current launcher options from storage.
        /// </summary>
        void LoadSettings();

        /// <summary>
        /// Provides a notification that the launcher options have been altered but not saved or
        /// are now committed to disk.
        /// </summary>
        event EventHandler<DirtyChangedEventArgs> DirtyChanged;

        /// <summary>
        /// Gets a win forms control which allow editing of the options.
        /// </summary>
        Control Control {
            get;
        }
    }
}
