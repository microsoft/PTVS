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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities {
    public class AssertListener : TraceListener {
        private readonly SynchronizationContext _testContext;

        private AssertListener() {
            _testContext = SynchronizationContext.Current;
        }

        public override string Name {
            get { return "AssertListener"; }
            set { }
        }

        public static void Initialize() {
            if (null == Debug.Listeners["AssertListener"]) {
                Debug.Listeners.Add(new AssertListener());
                Debug.Listeners.Remove("Default");

                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            }
        }

        static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e) {
            if (e.Exception is NullReferenceException || e.Exception is ObjectDisposedException) {
                var log = new EventLog("Application");
                log.Source = "Application Error";
                log.WriteEntry(
                    "First-chance exception: " + e.Exception.ToString(),
                    EventLogEntryType.Warning
                );
            }
        }

        public override void Fail(string message) {
            Fail(message, null);
        }

        public override void Fail(string message, string detailMessage) {
            Trace.WriteLine("Debug.Assert failed");
            if (!string.IsNullOrEmpty(message)) {
                Trace.WriteLine(message);
            } else {
                Trace.WriteLine("(No message provided)");
            }
            if (!string.IsNullOrEmpty(detailMessage)) {
                Trace.WriteLine(detailMessage);
            }
            var trace = new StackTrace(true);
            bool seenDebugAssert = false;
            foreach (var frame in trace.GetFrames()) {
                var mi = frame.GetMethod();
                if (!seenDebugAssert) {
                    seenDebugAssert = (mi.DeclaringType == typeof(Debug) && mi.Name == "Assert");
                } else if (mi.DeclaringType == typeof(System.RuntimeMethodHandle)) {
                    break;
                } else {
                    Trace.WriteLine(string.Format(
                        " at {0}.{1}({2}) in {3}:line {4}",
                        mi.DeclaringType.FullName,
                        mi.Name,
                        string.Join(", ", mi.GetParameters().Select(p => p.ToString())),
                        frame.GetFileName(),
                        frame.GetFileLineNumber()
                    ));
                    try {
                        Trace.WriteLine(
                            "    " +
                            File.ReadLines(frame.GetFileName()).ElementAt(frame.GetFileLineNumber() - 1).Trim()
                        );
                    } catch {
                    }
                }
            }

            message = string.IsNullOrEmpty(message) ? "Debug.Assert failed" : message;
            if (Debugger.IsAttached) {
                Debugger.Break();
            }

            if (_testContext != null && _testContext != SynchronizationContext.Current) {
                _testContext.Post(_ => Assert.Fail(message), null);
            } else {
                Assert.Fail(message);
            }
        }

        public override void WriteLine(string message) {
        }

        public override void Write(string message) {
        }
    }
}
