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

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities
{
    public class AssertListener : TraceListener
    {
        private bool _assertLogged = false;

        public override string Name
        {
            get { return "AssertListener"; }
            set { }
        }

        public static void Startup()
        {
            if (null == Debug.Listeners["AssertListener"])
            {
                Debug.Listeners.Add(new AssertListener());
            }
        }

        public static void ReportAsserts()
        {
            AssertListener currentListener = (AssertListener)Debug.Listeners["AssertListener"];
            if (currentListener != null && currentListener._assertLogged == true)
            {
                Assert.Fail("Test failed due to assertion. See Debug Trace for assertion callstack.");
                currentListener._assertLogged = false;
            }
        }

        public override void Fail(string message)
        {
            Fail(message, null);
        }

        public override void Fail(string message, string detailMessage)
        {
            Trace.WriteLine("Assertion info:\n");
            Trace.WriteLine(message);
            Trace.WriteLine(detailMessage);
            StackTrace trace = new StackTrace(true);
            Trace.WriteLine(trace);

            _assertLogged = true;
        }

        public override void WriteLine(string message)
        {
        }

        public override void Write(string message)
        {
        }
    }
}
