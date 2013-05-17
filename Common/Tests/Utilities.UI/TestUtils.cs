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
using System.Threading;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI {
    static public class TestUtils {
        public static void DteExecuteCommandOnThreadPool(string commandName, string commandArgs = "") {
            ThreadPool.QueueUserWorkItem((x) => {
                try {
                    VsIdeTestHostContext.Dte.ExecuteCommand(commandName, commandArgs);
                } catch (Exception e) {
                    Assert.Fail("Unexpected Exception - VsIdeTestHostContext.Dte.ExecuteCommand({0},{1}){2}{3}",
                        commandName, commandArgs, Environment.NewLine, e.ToString());
                }
            });
        }
    }
}
