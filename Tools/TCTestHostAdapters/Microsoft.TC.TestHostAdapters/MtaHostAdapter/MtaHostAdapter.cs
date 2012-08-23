/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Execution;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.VisualStudio.TestTools.Vsip;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// MTA Host Adapter
    /// Runs tests on MTA thread.
    /// 
    /// Registry settins that affect execution:
    /// Registry: HKCU\SOFTWARE\Microsoft\VisualStudio\10.0\EnterpriseTools\QualityTools\HostAdapters\TC MTA\
    /// </summary>
    [RegisterHostAdapter(Constants.MtaHostAdapterName, typeof(MtaHostAdapter), null)]
#if DEV11
    [DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\11.0")]
#else
    [DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\10.0")]
#endif
    // We don't need to define Dispose, as IDisposable children are managed objects and we call Dispose on them when we clean up.
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    internal class MtaHostAdapter : ITestAdapter
    {
        private IRunContext m_runContext;
        private TestRunConfiguration m_runConfig;
        private string m_workingDir;
        private Thread m_mtaThread;
        private Dictionary<string, ITestAdapter> m_adapters = new Dictionary<string, ITestAdapter>();

        /// <summary>
        /// Constructor. Called by Agent via Activator.CreateInstance and should not have any parameters.
        /// </summary>
        public MtaHostAdapter()
        {
            Trace.TraceInformation("Called MtaHostAdapter.ctor");
        }

        /// <summary>
        /// ITestAdapter method: called to initialize run context for this adapter.
        /// </summary>
        /// <param name="runContext">The run context to be used for this run</param>
        void ITestAdapter.Initialize(IRunContext runContext)
        {
            Trace.TraceInformation("Called MtaHostAdapter.Initialize");
            Debug.Assert(runContext != null);

            m_runContext = runContext;
            m_runConfig = m_runContext.RunConfig.TestRun.RunConfiguration;
            m_workingDir = m_runContext.RunContextVariables.GetStringValue("TestDeploymentDir");

            Debug.Assert(m_runConfig != null);
            Debug.Assert(!string.IsNullOrEmpty(m_workingDir));
            m_mtaThread = new Thread(MtaWorkerThreadMain);
            m_mtaThread.SetApartmentState(ApartmentState.MTA);
            m_completed = false;
            m_runStartEvent = new AutoResetEvent(false);
            m_runEndEvent = new AutoResetEvent(false);
            m_mtaThread.Start();

        }

        void MtaWorkerThreadMain()
        {
            while (true)
            {
                if (m_completed)
                {
                    break;
                }
                m_runStartEvent.WaitOne();
                if (m_completed)
                {
                    break;
                }
                lock (m_syncObject)
                {
                    m_mtaException = null;
                    try
                    {
                        m_mtaCode();
                    }
                    catch (Exception ex)
                    {
                        m_mtaException = ex;
                    }
                    m_runEndEvent.Set();
                }
            }
        }

        void RunOnMtaThread(Action mtaCode)
        {
            lock (m_syncObject)
            {
                m_mtaCode = mtaCode;
                m_mtaException = null;
                m_runStartEvent.Set();
            }
            m_runEndEvent.WaitOne();
            Exception caughtException = null;
            lock (m_syncObject)
            {
                caughtException = m_mtaException;
                m_mtaCode = null;
                m_mtaException = null;
            }
            if (caughtException != null)
            {
                throw caughtException;
            }
        }

        private object m_syncObject = new Object();
        private Action m_mtaCode;
        private Exception m_mtaException;
        private bool m_completed;
        private AutoResetEvent m_runStartEvent;
        private AutoResetEvent m_runEndEvent;

        /// <summary>
        /// IBaseAdapter method: called to execute a test.
        /// </summary>
        /// <param name="testElement">The test object to run</param>
        /// <param name="testContext">The Test conext for this test invocation</param>
        void IBaseAdapter.Run(ITestElement testElement, ITestContext testContext)
        {
            Trace.TraceInformation("Called MtaHostAdapter.Run");
            ITestAdapter realAdapter = GetTestAdapter(testElement);

            RunOnMtaThread(() =>
                {
                    realAdapter.Run(testElement, testContext);
                });

        }


        /// <summary>
        /// IBaseAdapter method: called when the test run is complete.
        /// </summary>
        void IBaseAdapter.Cleanup()
        {
            Trace.TraceInformation("Called MtaHostAdapter.Run");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.Cleanup();
            }
            m_adapters.Clear();
            m_completed = true;
            m_runStartEvent.Set();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user stops the test run.
        /// </summary>
        void IBaseAdapter.StopTestRun()
        {
            Trace.TraceInformation("Called MtaHostAdapter.StopTestRun");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.StopTestRun();
            }
            m_completed = true;
            m_runStartEvent.Set();
        }

        /// <summary>
        /// IBaseAdapter method: called when the test run is aborted.
        /// </summary>
        void IBaseAdapter.AbortTestRun()
        {
            Trace.TraceInformation("Called MtaHostAdapter.AbortTestRun");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.AbortTestRun();
            }
            m_completed = true;
            m_runStartEvent.Set();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user pauses the test run.
        /// </summary>
        void IBaseAdapter.PauseTestRun()
        {
            Trace.TraceInformation("Called MtaHostAdapter.PauseTestRun");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.PauseTestRun();
            }
        }

        /// <summary>
        /// IBaseAdapter method: called when the user resumes a paused test run.
        /// </summary>
        void IBaseAdapter.ResumeTestRun()
        {
            Trace.TraceInformation("Called MtaHostAdapter.ResumeTestRun");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.ResumeTestRun();
            }
        }

        /// <summary>
        /// ITestAdapter method: called when a message is sent from the UI or the controller.
        /// </summary>
        /// <param name="obj">The message object</param>
        void ITestAdapter.ReceiveMessage(object obj)
        {
            Trace.TraceInformation("Called MtaHostAdapter.ReceiveMessage");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.ReceiveMessage(obj);
            }
        }

        /// <summary>
        /// ITestAdapter method: called just before the test run finishes and
        /// gives the adapter a chance to do any clean-up.
        /// </summary>
        /// <param name="runContext">The run context for this run</param>
        void ITestAdapter.PreTestRunFinished(IRunContext runContext)
        {
            Trace.TraceInformation("Called MtaHostAdapter.PreTestRunFinished");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.PreTestRunFinished(runContext);
            }
            m_completed = true;
            m_runStartEvent.Set();
        }

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


    }
}
