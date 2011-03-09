using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.TestAdapter;

namespace Microsoft.TC.RemoteTest
{
    [ComVisible(true)]
    [Guid(RemoteTest.IRemoteTestGuid)]
    public interface IRemoteTest
    {
        ITestAdapter GetHostSide();
        int GetHostProcessId();
        void SetCurrentDirectory(string currentDirectory);
    }
}
