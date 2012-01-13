/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.VisualStudio.TestTools.Common;
using Microsoft.VisualStudio.TestTools.Execution;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;
using Microsoft.TC.TestHostAdapters;
using System.Security.Permissions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Remoting;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// Represents Host Side of the Host Adapter as this object lives inside Visual Studio process.
    /// This is MarshalByRefObject wrapper for Test Adapter, since not all Test Adapters could be MarshalByRefObject.
    /// </summary>
    internal sealed class HostAdapterHostSide : MarshalByRefObject, ITestAdapter, IDisposable
    {
        #region Fields
        private Dictionary<string, ITestAdapter> m_adapters = new Dictionary<string, ITestAdapter>();
        private IRunContext m_runContext;
        private IServiceProvider m_serviceProvider;
        #endregion

        #region Constructors
        /// <summary>
        /// Disabled costructor. Nobody is supposed to call it.
        /// </summary>
        private HostAdapterHostSide()
        {
            Debug.Fail("HostAdapterHostSide.HostAdapterHostSide(): nobody should call this.");
        }

        /// <summary>
        /// Constructor. Called by the Addin.
        /// </summary>
        /// <param name="serviceProvider">VS Service provider.</param>
        internal HostAdapterHostSide(IServiceProvider serviceProvider)
        {
            Debug.Assert(serviceProvider != null, "HostAdapterHostSide.HostAdapterHostSide: serverProvider is null!");
            m_serviceProvider = serviceProvider;

            // Initialize the Framework.
            VsIdeTestHostContext.ServiceProvider = m_serviceProvider;
            UIThreadInvoker.Initialize();
        }
        #endregion

        #region IBaseAdapter, ITestAdapter
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "0#")]
        void ITestAdapter.Initialize(IRunContext runContext)
        {
            // We delay inner TAs initialization until Run method because we don't know which test type this is going to be.
            m_runContext = runContext;
        }

        void IBaseAdapter.Run(ITestElement testElement, ITestContext testContext)
        {
            ITestAdapter realAdapter = GetTestAdapter(testElement);

            realAdapter.Run(testElement, testContext);
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
        #endregion

        #region IDisposable
        /// <summary>
        /// IDisposable.Dispose implmentation.
        /// Unregister the object from .Net Remoting so that Garbage Collector can collect it.
        /// </summary>
        [SecurityPermission(SecurityAction.Demand, Infrastructure = true)]    // RemotingServices.Disconnect has LinkDemand for Infrastructure.
        public void Dispose()
        {
            RemotingServices.Disconnect(this);
        }
        #endregion

        #region Private
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
        #endregion
    }
}
