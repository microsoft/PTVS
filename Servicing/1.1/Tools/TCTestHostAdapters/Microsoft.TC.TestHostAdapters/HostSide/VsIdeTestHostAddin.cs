/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters;
using System.Security.Principal;
using System.Runtime.Remoting;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.TestAdapter;
using Microsoft.VisualStudio.TestTools.Vsip;
using Microsoft.Win32;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.TC.TestHostAdapters
{
    /// <summary>
    /// Interface used by Host Adapter (Agent side) to get the Addin from Visual Studio process.
    /// </summary>
    [ComVisible(true)]
    [Guid(Constants.IVsIdeTestHostAddinGuidString)]
    [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "Vs")]
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Addin")]
    public interface IVsIdeTestHostAddin
    {
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]   // This is not a simle property and does things behind the scene.
        ITestAdapter GetHostSide();
        object RemoteExecute(string path, string className, string FunctionName, params object[] objs);
        int GetHostProcessId();
        
    }

    /// <summary>
    /// The object implementing Visual Studio Addin.
    /// Hosts an instance of HostAdapterHostSide that is used to drive/run tests in Visual Studio process.
    /// </summary>
    /// <seealso class='IDTExtensibility2' />
    [Guid(Constants.VsIdeTestHostAddinGuidString)]
    [ComVisible(true)]
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]  // Lifetime is managed by VS Addin manager, so don't do IDisposable.
    [SuppressMessage("Microsoft.Naming", "CA1706:ShortAcronymsShouldBeUppercase")]  // Be consistent with VS Primary Interop Assemblies.
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Addin")]
    public sealed class VsIdeTestHostAddin : MarshalByRefObject, IDTExtensibility2, IVsIdeTestHostAddin
    {
        private DTE2 m_applicationObject;
        private IChannel m_serverChannel;
        private IChannel m_clientChannel;
        private HostAdapterHostSide m_hostSide;
        private ServiceProvider m_serviceProvider;
        private object m_hostLock = new object();
        private ManualResetEvent m_hostInitializedEvent = new ManualResetEvent(false);
        private bool m_hostInitialized; // Whether VS we use to run the tests is initialized; static in case if VS creaes 2 instances.

        [SuppressMessage("Microsoft.Performance", "CA1802:UseLiteralsWhereAppropriate")]     // Cannot use const for initializer that calls a property.
        private static readonly int s_onConnectionTimeout = RegistrySettings.BaseTimeout * 5;

        /// <summary>Constructor.</summary>
        public VsIdeTestHostAddin()
        {
        }

        /// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
        /// <param term='application'>Root object of the host application.</param>
        /// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
        /// <param term='addInInst'>Object representing this Add-in.</param>
        /// <seealso class='IDTExtensibility2' />
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "0#")]
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "1#")]
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "2#")]
        [CLSCompliant(false)]
        [SecurityPermission(SecurityAction.Demand, Unrestricted = true)] // Process.Id has LinkDemand for Unrestricted.
        public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
        {
            Trace.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "[pid={0,4}, tid={1,2}, {2:yyyy}/{2:MM}/{2:dd} {2:HH}:{2:mm}:{2:ss}.{2:fff}] VsIdeTestHostAddin.OnConnection", 
                System.Diagnostics.Process.GetCurrentProcess().Id, 
                System.Threading.Thread.CurrentThread.ManagedThreadId, 
                DateTime.Now));
        
            if (RegistryHelper<int>.GetValueIgnoringExceptions(
                    Registry.CurrentUser, RegistrySettings.HostAdapterRegistryKeyName,
                    "EnableDebugBreak",
                    0) == 1)
            {
                System.Diagnostics.Debugger.Break();
            }

            // The idea about connection modes is to make sure we are initialized
            // after 1st OnConnection is called. Because this is when VS thinks that
            // addin is ready and returns it to outside.
            if (connectMode == ext_ConnectMode.ext_cm_UISetup ||    // When VS sets up UI for Addin.
                connectMode == ext_ConnectMode.ext_cm_Startup ||    // When VS is started.
                connectMode == ext_ConnectMode.ext_cm_AfterStartup) // When loading from Tools->Addin Manager.
            {
                try
                {
                    lock (m_hostLock)   // Protect from calling with different modes at the same time.
                    {
                        DTE2 dteApplication = (DTE2)application;
                        Debug.Assert(dteApplication != null, "OnConnect: (DTE2)application = null!");

                        if (!m_hostInitialized) // When loading from Tools->Addin Manager.
                        {
                            m_applicationObject = dteApplication;

                            SetupChannels();

                            InitHostSide();

                            m_hostInitialized = true;
                            m_hostInitializedEvent.Set();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Fail("Addin.OnConnection: " + ex.ToString());
                    throw;
                }
            }
        }

        /// <summary>
        /// Initialize the Host Side.
        /// This can be called from OnConnect by Addin AND from GetHostSide by Host Adapter.
        /// </summary>
        private void InitHostSide()
        {
            try
            {
                lock (m_hostLock)
                {
                    if (m_hostSide == null)
                    {
                        Debug.Assert(m_applicationObject != null, "HostSide.InitHostSide: m_applicationObject is null!");

                        m_serviceProvider = new ServiceProvider((IOleServiceProvider)m_applicationObject);
                        Debug.Assert(m_serviceProvider != null, "VsIdeTestHostAddin.InitHostSide: failed to init service provider!");

                        m_hostSide = new HostAdapterHostSide(m_serviceProvider);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Fail("HA.InitHostSide: " + ex.ToString());
                throw;
            }
        }

        /// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
        /// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", MessageId = "0#")]
        [CLSCompliant(false)]
        [SecurityPermission(SecurityAction.Demand, Infrastructure = true)]    // RemotingServices.Disconnect has LinkDemand for Infrastructure.
        public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
        {
            m_hostSide.Dispose();
            CleanupChannels();
            RemotingServices.Disconnect(this);
        }

        /// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />		
        public void OnAddInsUpdate(ref Array custom)
        {
        }

        /// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnStartupComplete(ref Array custom)
        {
        }

        /// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnBeginShutdown(ref Array custom)
        {
        }

        /// <summary>
        /// Obtains the Host Side.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]   // This is not a simle property and does things behind the scene.
        ITestAdapter IVsIdeTestHostAddin.GetHostSide()
        {
            // Wait for OnConnection to initialize the addin.
            bool waitSucceeded = m_hostInitializedEvent.WaitOne(s_onConnectionTimeout, false);
            Debug.Assert(waitSucceeded, "Timed out waiting for OnConnection to initialize.");

            lock (m_hostLock)
            {
                Debug.Assert(m_hostInitialized, "Addin.GetHostSide: m_hostInitialized is false!");

                InitHostSide();
                Debug.Assert(m_hostSide != null, "m_hostSide is null!");
            }

            return m_hostSide;
        }
        
        object IVsIdeTestHostAddin.RemoteExecute(string path, string className, string FunctionName, params object[] objs)
        {
            object retObject = null;
            Assembly testAssembly = null;
            Type typ = null;

            try
            {
                testAssembly = Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                throw new Exception("Exception during load assembly", e);
            }

            typ = testAssembly.GetType(className);
            if (typ == null)
                throw new Exception(string.Format("Class {0} not found in test assembly {1}", className, testAssembly.FullName));

            MethodInfo mi = typ.GetMethod(FunctionName);
            if (mi == null)
                throw new Exception(string.Format("Method {0} not found in class {1}. Check the VS method name to Invoke for typos", FunctionName, className));

            try
            {
                // get required method by specifying name and invoke it
                retObject = mi.Invoke(0, objs);
            }
            catch (ArgumentException e)
            {
                throw new Exception("ArgumentException during invokation of VS method - check your arguments to Invoke", e);
            }
            catch (Exception e)
            {
                throw new Exception("Exception during invokation of VS Method", e);
            }

            return retObject;
        }

        int IVsIdeTestHostAddin.GetHostProcessId()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        /// <summary>
        /// Set up remoting communication channels.
        /// </summary>
        private void SetupChannels()
        {
            lock (m_hostLock)
            {
                // If channels are not set up yet, set them up.
                if (m_serverChannel == null)
                {
                    Debug.Assert(m_clientChannel == null);

                    // This channel is used for debugging session, when TA connects to authoring vs instance.
                    BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();
                    serverProvider.TypeFilterLevel = TypeFilterLevel.Full;  // Enable remoting objects as arguments.
                    Hashtable props = new Hashtable();
                    string channelNamePrefix = "EqtVsIdeHostAdapterAddin_";
                    string serverChannelName = channelNamePrefix + Guid.NewGuid() + "_ServerChannel";
                    props["name"] = serverChannelName;
                    props["portName"] = serverChannelName;           // Must be different from client's port.
                    // Default IpcChannel security is: allow for all users who can authorize on this machine.
                    props["authorizedGroup"] = WindowsIdentity.GetCurrent().Name;
                    m_serverChannel = new IpcServerChannel(props, serverProvider);
                    ChannelServices.RegisterChannel(m_serverChannel, false);

                    // This channel is used for connecting to both authoring and host side VS.
                    m_clientChannel = new IpcClientChannel(channelNamePrefix + Guid.NewGuid() + "_ClientChannel", new BinaryClientFormatterSinkProvider());
                    ChannelServices.RegisterChannel(m_clientChannel, false);
                }
            }
        }

        /// <summary>
        /// Clean up communication channels.
        /// </summary>
        private void CleanupChannels()
        {
            lock (m_hostLock)
            {
                if (m_serverChannel != null)
                {
                    ChannelServices.UnregisterChannel(m_serverChannel);
                    m_serverChannel = null;
                }

                if (m_clientChannel != null)
                {
                    ChannelServices.UnregisterChannel(m_clientChannel);
                    m_clientChannel = null;
                }
            }
        }

        /// <summary>
        /// Controls lifetime of the object. 
        /// We return null which means infinite lifetime since we in OnDisconnect to manually unregister the object from .Net remoting.
        /// </summary>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
