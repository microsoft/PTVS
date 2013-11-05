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
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.BuildTasks {
    /// <summary>
    /// Converts filenames to Python module names.
    /// </summary>
    public class ConvertPathToModuleName : Task {
        /// <summary>
        /// The filenames to convert.
        /// </summary>
        [Required]
        public ITaskItem[] Paths { get; set; }

        /// <summary>
        /// The path representing the top-level module. Even if there are more
        /// __init__.py files above this path, they will not become part of the
        /// module name.
        /// </summary>
        public string PathLimit { get; set; }

        /// <summary>
        /// The converted module names.
        /// </summary>
        [Output]
        public ITaskItem[] ModuleNames { get; private set; }

        public override bool Execute() {
            var modules = new List<ITaskItem>();

            foreach (var path in Paths) {
                try {
                    modules.Add(new TaskItem(ModulePath.FromFullPath(path.ItemSpec, PathLimit).ModuleName));
                } catch (ArgumentException ex) {
                    Log.LogErrorFromException(ex);
                }
            }

            ModuleNames = modules.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}
