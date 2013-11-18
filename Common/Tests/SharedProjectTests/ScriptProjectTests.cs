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

using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;

namespace Microsoft.VisualStudioTools.SharedProjectTests {
    /// <summary>
    /// Test cases which are applicable to projects designed for scripting languages.
    /// </summary>
    [TestClass]
    public class ScriptProjectTests : SharedProjectTest {
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RunWithoutStartupFile() {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("RunWithoutStartupFile", projectType);

                using (var solution = testDef.Generate().ToVs()) {
                    solution.App.OpenDialogWithDteExecuteCommand("Debug.Start");
                    VisualStudioApp.CheckMessageBox(
                        "No startup file is defined for the startup project."
                    );

                    solution.App.OpenDialogWithDteExecuteCommand("Debug.StartWithoutDebugging");
                    VisualStudioApp.CheckMessageBox(
                        "No startup file is defined for the startup project."
                    );
                }
            }
        }
    }
}
