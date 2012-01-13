using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.TestAdapter;

namespace Microsoft.TC.RemoteTest
{
    /// <summary>
    /// Interface used by Host Adapter (Agent side) to get the Addin 
    /// </summary>
    [ComVisible(true)]
    [Guid(RemoteTest.RemoteTestComponentGuid)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class RemoteTestComponent : MarshalByRefObject, IRemoteTest
    {
        private IChannel m_serverChannel;
        private IChannel m_clientChannel;
        private RemoteTestAdapter m_hostSide;
        private object m_hostLock = new object();

        /// <summary>Constructor.</summary>
        public RemoteTestComponent()
        {
            SetupChannels();
            m_hostSide = new RemoteTestAdapter();
        }

        /// <summary>
        /// Obtains the Host Side.
        /// </summary>
        ITestAdapter IRemoteTest.GetHostSide()
        {
            return m_hostSide;
        }

        int IRemoteTest.GetHostProcessId()
        {
            return System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        void IRemoteTest.SetCurrentDirectory(string currentDirectory)
        {
            Environment.CurrentDirectory = currentDirectory;
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
                    string channelNamePrefix = "ExcelTestHostAddin_";
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
    }
}
