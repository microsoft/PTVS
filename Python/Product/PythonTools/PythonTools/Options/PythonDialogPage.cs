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
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Base class used for saving/loading of settings.  The settings are stored in VSRegistryRoot\PythonTools\Options\Category\SettingName
    /// where Category is provided in the constructor and SettingName is provided to each call of the Save*/Load* APIs.
    /// x = 42
    /// 
    /// The primary purpose of this class is so that we can be in control of providing reasonable default values.
    /// </summary>
    [ComVisible(true)]
    public class PythonDialogPage : DialogPage {
        private readonly string _category;
        private const string _optionsKey = "Options";

        internal PythonDialogPage(string category) {
            _category = category;
        }

        internal PythonToolsService PyService {
            get {
                return ((IServiceProvider)this).GetPythonToolsService();
            }
        }

        internal IComponentModel ComponentModel {
            get {
                return ((IServiceProvider)this).GetComponentModel();
            }
        }
    }
}
