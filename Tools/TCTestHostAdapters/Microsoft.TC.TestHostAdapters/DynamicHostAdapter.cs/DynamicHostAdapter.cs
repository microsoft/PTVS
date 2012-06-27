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
    [RegisterHostAdapter(Constants.DynamicHostAdapterName, typeof(DynamicHostAdapter), null)]
    [DefaultRegistryRoot(@"Software\Microsoft\VisualStudio\11.0")]
    // We don't need to define Dispose, as IDisposable children are managed objects and we call Dispose on them when we clean up.
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    internal class DynamicHostAdapter : ITestAdapter
    {
        private IRunContext m_runContext;
        private TestRunConfiguration m_runConfig;
        private string m_workingDir;
        private Dictionary<string, ITestAdapter> m_adapters = new Dictionary<string, ITestAdapter>();

        /// <summary>
        /// Constructor. Called by Agent via Activator.CreateInstance and should not have any parameters.
        /// </summary>
        public DynamicHostAdapter()
        {
            Trace.TraceInformation("Called DynamicHostAdapter.ctor");
        }

        /// <summary>
        /// ITestAdapter method: called to initialize run context for this adapter.
        /// </summary>
        /// <param name="runContext">The run context to be used for this run</param>
        void ITestAdapter.Initialize(IRunContext runContext)
        {
            Trace.TraceInformation("Called DynamicHostAdapter.Initialize");
            Debug.Assert(runContext != null);

            m_runContext = runContext;
            m_runConfig = m_runContext.RunConfig.TestRun.RunConfiguration;
            m_workingDir = m_runContext.RunContextVariables.GetStringValue("TestDeploymentDir");

            Debug.Assert(m_runConfig != null);
            Debug.Assert(!string.IsNullOrEmpty(m_workingDir));

        }



        /// <summary>
        /// IBaseAdapter method: called to execute a test.
        /// </summary>
        /// <param name="testElement">The test object to run</param>
        /// <param name="testContext">The Test conext for this test invocation</param>
        void IBaseAdapter.Run(ITestElement testElement, ITestContext testContext)
        {
            Trace.TraceInformation("Called DynamicHostAdapter.Run");
            ITestAdapter realAdapter = GetTestAdapter(testElement);

            realAdapter.Run(testElement, testContext);

        }


        /// <summary>
        /// IBaseAdapter method: called when the test run is complete.
        /// </summary>
        void IBaseAdapter.Cleanup()
        {
            Trace.TraceInformation("Called DynamicHostAdapter.Run");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.Cleanup();
            }
            m_adapters.Clear();
        }

        /// <summary>
        /// IBaseAdapter method: called when the user stops the test run.
        /// </summary>
        void IBaseAdapter.StopTestRun()
        {
            Trace.TraceInformation("Called DynamicHostAdapter.StopTestRun");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.StopTestRun();
            }
        }

        /// <summary>
        /// IBaseAdapter method: called when the test run is aborted.
        /// </summary>
        void IBaseAdapter.AbortTestRun()
        {
            Trace.TraceInformation("Called DynamicHostAdapter.AbortTestRun");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.AbortTestRun();
            }
        }

        /// <summary>
        /// IBaseAdapter method: called when the user pauses the test run.
        /// </summary>
        void IBaseAdapter.PauseTestRun()
        {
            Trace.TraceInformation("Called DynamicHostAdapter.PauseTestRun");
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
            Trace.TraceInformation("Called DynamicHostAdapter.ResumeTestRun");
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
            Trace.TraceInformation("Called DynamicHostAdapter.ReceiveMessage");
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
            Trace.TraceInformation("Called DynamicHostAdapter.PreTestRunFinished");
            foreach (ITestAdapter testAdapter in m_adapters.Values)
            {
                testAdapter.PreTestRunFinished(runContext);
            }
        }

        private ITestAdapter GetTestAdapter(ITestElement test)
        {
            Debug.Assert(test != null, "Internal error: test is null!");

            // Get more metadata off the type.
            string id = test.HumanReadableId;
            int lastDot = id.LastIndexOf('.');
            string typeName = id.Substring(0, lastDot);
            Type dynamicHostType = DynamicHostTypeAttribute.GetDynamicHostType(test.Name, typeName, test.Storage);
            string dynamicHostTypeName = dynamicHostType.FullName;

            ITestAdapter realTestAdapter = null;
            bool containsAdapter = m_adapters.TryGetValue(dynamicHostTypeName, out realTestAdapter);
            if (!containsAdapter)
            {
                realTestAdapter = (ITestAdapter)Activator.CreateInstance(dynamicHostType, new Object[] { });

                // Iniitialize was delayed to be run from the Run method.
                realTestAdapter.Initialize(m_runContext);

                m_adapters.Add(dynamicHostTypeName, realTestAdapter);
            }

            return realTestAdapter;
        }


    }
}
