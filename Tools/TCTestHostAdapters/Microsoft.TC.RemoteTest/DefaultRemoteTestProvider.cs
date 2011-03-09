using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Microsoft.TC.RemoteTest
{
    [ComVisible(true)]
    [Guid(RemoteTest.DefaultRemoteTestProviderGuid)]
    [ClassInterface(ClassInterfaceType.None)]
    public class DefaultRemoteTestProvider: MarshalByRefObject, IRemoteTestProvider
    {
        private IRemoteTest m_remoteTestComponent = new RemoteTestComponent();

        public DefaultRemoteTestProvider()
        {
        }

        public DefaultRemoteTestProvider(object hostApplication)
        {
            RemoteTest.InitializeInProcess(hostApplication);
        }

        IRemoteTest IRemoteTestProvider.GetRemoteTestComponent()
        {
            return m_remoteTestComponent;
        }
    }
}
