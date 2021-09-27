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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace TestUtilities
{
    public class AssertListener : TraceListener
    {
        private readonly SynchronizationContext _testContext;
        private readonly List<ExceptionDispatchInfo> _unhandled = new List<ExceptionDispatchInfo>();

        private AssertListener()
        {
            _testContext = SynchronizationContext.Current;
        }

        public override string Name
        {
            get { return "Microsoft.PythonTools.AssertListener"; }
            set { }
        }

        public static bool LogObjectDisposedExceptions { get; set; } = true;

        public static void Initialize()
        {
            var listener = new AssertListener();
            if (null == Debug.Listeners[listener.Name])
            {
                Debug.Listeners.Add(listener);
                Debug.Listeners.Remove("Default");

                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            }
        }

        public static void ThrowUnhandled()
        {
            var ex = Debug.Listeners.OfType<AssertListener>().SelectMany(al =>
            {
                lock (al._unhandled)
                {
                    var r = al._unhandled.ToArray();
                    al._unhandled.Clear();
                    return r;
                }
            }).ToArray();
            if (ex.Length > 1)
            {
                foreach (var e in ex)
                {
                    Console.Error.WriteLine(e.SourceException.ToString());
                }
                throw new AggregateException(ex.Select(e => e.SourceException));
            }
            else if (ex.Length == 1)
            {
                Console.Error.WriteLine(ex[0].SourceException.ToString());
                ex[0].Throw();
            }
        }

        static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is NullReferenceException || (e.Exception is ObjectDisposedException && LogObjectDisposedExceptions))
            {
                // Exclude safe handle messages because they are noisy
                if (!e.Exception.Message.Contains("Safe handle has been closed"))
                {
                    var log = new EventLog("Application");
                    log.Source = "Application Error";
                    log.WriteEntry(
                        "First-chance exception: " + e.Exception.ToString(),
                        EventLogEntryType.Warning
                    );
                }
            }
        }

        public override void Fail(string message)
        {
            Fail(message, null);
        }

        public override void Fail(string message, string detailMessage)
        {
            Trace.WriteLine("Debug.Assert failed");
            if (!string.IsNullOrEmpty(message))
            {
                Trace.WriteLine(message);
            }
            else
            {
                Trace.WriteLine("(No message provided)");
            }
            if (!string.IsNullOrEmpty(detailMessage))
            {
                Trace.WriteLine(detailMessage);
            }
            var trace = new StackTrace(true);
            bool seenDebugAssert = false;
            foreach (var frame in trace.GetFrames())
            {
                var mi = frame.GetMethod();
                if (!seenDebugAssert)
                {
                    seenDebugAssert = (mi.DeclaringType == typeof(Debug) &&
                        (mi.Name == "Assert" || mi.Name == "Fail"));
                }
                else if (mi.DeclaringType == typeof(System.RuntimeMethodHandle))
                {
                    break;
                }
                else
                {
                    var filename = frame.GetFileName();
                    Console.Error.WriteLine(string.Format(
                        " at {0}.{1}({2}) in {3}:line {4}",
                        mi.DeclaringType.FullName,
                        mi.Name,
                        string.Join(", ", mi.GetParameters().Select(p => p.ToString())),
                        filename ?? "<unknown>",
                        frame.GetFileLineNumber()
                    ));
                    if (File.Exists(filename))
                    {
                        try
                        {
                            Console.Error.WriteLine(
                                "    " +
                                File.ReadLines(filename).ElementAt(frame.GetFileLineNumber() - 1).Trim()
                            );
                        }
                        catch
                        {
                        }
                    }
                }
            }

            message = string.IsNullOrEmpty(message) ? "Debug.Assert failed" : message;
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            Console.Error.WriteLine(message);

            if (_testContext == null)
            {
                lock (_unhandled)
                {
                    try
                    {
                        Assert.Fail(message);
                    }
                    catch (AssertFailedException ex)
                    {
                        _unhandled.Add(ExceptionDispatchInfo.Capture(ex));
                    }
                }
            }
            else if (_testContext != SynchronizationContext.Current)
            {
                _testContext.Post(_ => Assert.Fail(message), null);
            }
            else
            {
                Assert.Fail(message);
            }
        }

        public override void WriteLine(string message)
        {
        }

        public override void Write(string message)
        {
        }
    }
}
