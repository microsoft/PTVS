using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Security.Permissions;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Execution;
using Microsoft.VisualStudio.TestTools.TestAdapter;

namespace Microsoft.TC.RemoteTest
{
    public class RemoteTestAdapter:  MarshalByRefObject, ITestAdapter, IDisposable
    {

        private Dictionary<string, ITestAdapter> m_adapters = new Dictionary<string, ITestAdapter>();
        private IRunContext m_runContext;

        public RemoteTestAdapter()
        {
        }

        void ITestAdapter.Initialize(IRunContext runContext)
        {
            // We delay inner TAs initialization until Run method because we don't know which test type this is going to be.
            m_runContext = runContext;
        }

        private delegate void ThreadInvoker();

        void IBaseAdapter.Run(ITestElement testElement, ITestContext testContext)
        {
            UIThreadInvoker.Initialize();
            UIThreadInvoker.Invoke((ThreadInvoker)delegate()
                {
                    ITestAdapter realAdapter = GetTestAdapter(testElement);

                    realAdapter.Run(testElement, testContext);
                    Trace.TraceInformation("Completed UI thread call to Run");
                });
            Trace.TraceInformation("Completed incoming call to Run");
        }

        void IBaseAdapter.Cleanup()
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.Cleanup();
            }
            m_adapters.Clear();
        }

        void IBaseAdapter.StopTestRun()
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.StopTestRun();
            }
        }

        void IBaseAdapter.AbortTestRun()
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.AbortTestRun();
            }
        }

        void IBaseAdapter.PauseTestRun()
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.PauseTestRun();
            }
        }

        void IBaseAdapter.ResumeTestRun()
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.ResumeTestRun();
            }
        }

        void ITestAdapter.ReceiveMessage(object obj)
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.ReceiveMessage(obj);
            }
        }

        void ITestAdapter.PreTestRunFinished(IRunContext runContext)
        {
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.PreTestRunFinished(runContext);
            }
        }

        /// <summary>
        /// IDisposable.Dispose implmentation.
        /// Unregister the object from .Net Remoting so that Garbage Collector can collect it.
        /// </summary>
        public void Dispose()
        {
            RemotingServices.Disconnect(this);
        }

        /// <summary>
        /// Get real Test Adapter from the test.
        /// </summary>
        /// <remarks>
        /// We call ITestElement.Adapter a real test adapter because Host Adapter server as a Test Adapter as well.
        /// </remarks>
        private ITestAdapter GetTestAdapter(ITestElement test)
        {
            Debug.Assert(test != null, "Internal error: test is null!");
            Debug.Assert(!string.IsNullOrEmpty(test.Adapter), "Internal error: test.Adapter is null or empty!");

            ITestAdapter realTestAdapter = null;
            bool containsAdapter = m_adapters.TryGetValue(test.Adapter, out realTestAdapter);
            if (!containsAdapter)
            {
                realTestAdapter = (ITestAdapter)Activator.CreateInstance(Type.GetType(test.Adapter), new Object[] { });

                // Iniitialize was delayed to be run from the Run method.
                realTestAdapter.Initialize(m_runContext);

                m_adapters.Add(test.Adapter, realTestAdapter);
            }

            return realTestAdapter;
        }

        /// <summary>
        /// Controls lifetime for of this instance.
        /// We return null which means infinite lifetime, as we manually control the lifetime by Dispose.
        /// </summary>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }


    }
}
