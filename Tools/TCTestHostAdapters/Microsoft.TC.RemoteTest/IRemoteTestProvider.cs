using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.TestAdapter;

namespace Microsoft.TC.RemoteTest
{
    [ComVisible(true)]
    [Guid(RemoteTest.IRemoteTestProviderGuid)]
    public interface IRemoteTestProvider
    {
        IRemoteTest GetRemoteTestComponent();
    }
}
