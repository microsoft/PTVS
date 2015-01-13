/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Runtime.Serialization.Formatters;
using System.Security.Principal;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.Vsip;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudioTools.VSTestHost.Internal {
    [PackageRegistration(UseManagedResourcesOnly = true, RegisterUsing=RegistrationMethod.Assembly)]
    [InstalledProductRegistration("#110", "#112", "1.0.1", IconResourceID = 400)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string)]
#if SUPPORT_TESTER
    [RegisterHostAdapter("VSTestHost", typeof(TesterTestAdapter), typeof(TesterTestControl))]
    [RegisterSupportedTestType("VSTestHost", "VS Test Host Adapter", Guids.UnitTestTypeString, "Unit Test")]
#endif
    [Guid(Guids.VSTestHostPkgString)]
    public sealed class VSTestHostPackage : Package
#if PACKAGE_NEEDS_DISPOSE
    , IDisposable
#endif
    {
        private IChannel _channel;

        public VSTestHostPackage() { }

        #if PACKAGE_NEEDS_DISPOSE
        // Package does not implement IDisposable, so define Dispose
        public void Dispose() {
            {
        #else
        // Package implements IDisposable, so override Dispose
        protected override void Dispose(bool disposing) {
            if (disposing) {
        #endif
                var channel = Interlocked.Exchange(ref _channel, null);
                if (channel != null) {
                    ChannelServices.UnregisterChannel(channel);
                }
            }
        }

        protected override void Initialize() {
#if SUPPORT_TESTEE
            // Configure our IPC channel for this process and register any
            // public services. The name is keyed off the module name and
            // process ID so our host can connect to a specific instance.
            _channel = RegisterChannel(GetChannelName(Process.GetCurrentProcess()));

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(TesteeTestAdapter),
                "vstest",
                WellKnownObjectMode.Singleton
            );
#endif

#if SUPPORT_TESTER
            // Register our DebugAttacher service so our test adapter can tell
            // us to attach to our client.
            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(TesterDebugAttacher),
                "debug",
                WellKnownObjectMode.SingleCall
            );

            // Register for notifications when we start debugging. This is used
            // so we can start listening for incoming calls to DebugAttacher
            // only when necessary.
            UIContext.FromUIContextGuid(new Guid(UIContextGuids.Debugging)).UIContextChanged += DebuggingChanged;
#endif

            base.Initialize();

#if SUPPORT_TESTEE
            // Initialize the global context used by tests to access VS services
            // and a DTE instance.
            VSTestContext.ServiceProvider = ServiceProvider.GlobalProvider;
            VSTestContext.IsMock = false;
#endif
        }

#if SUPPORT_TESTER
        async void DebuggingChanged(object sender, UIContextChangedEventArgs e) {
            if (e.Activated) {
                // We just started debugging, so publish the DebugAttacher class
                // and wait for a request.
                IChannel channel;
                EventWaitHandle notifyEvent;
                if (!TesterDebugAttacherShared.OpenChannel(10, out channel, out notifyEvent)) {
                    return;
                }
                try {
                    // Wait for the signal that we've either attached to our
                    // client, or we've aborted the wait. All we do after this
                    // is unregister the channel, so it doesn't really matter
                    // which it is.
                    var signaled = await Task.Run(() => notifyEvent.WaitOne(TimeSpan.FromSeconds(30)));
                } finally {
                    notifyEvent.Dispose();
                    TesterDebugAttacherShared.CloseChannel(channel);
                }
            } else {
                // We were debugging, but now we've stopped

                // Abort waiting in case we stopped within the timeout
                TesterDebugAttacherShared.CancelWait();
            }
        }
#endif

        public static string GetChannelName(Process process) {
            return string.Format("VSTestHost_{0}_{1:X8}_06420E12_C5A1_4EEF_B604_406E6A139737",
                process.MainModule.ModuleName.ToLowerInvariant(),
                process.Id
            );
        }

        internal static IChannel RegisterChannel(string name) {
            var properties = new Dictionary<string, string> {
                { "name", name },
                { "portName", name },
                { "authorizedGroup", WindowsIdentity.GetCurrent().Name }
            };

            var serverProvider = new BinaryServerFormatterSinkProvider();
            serverProvider.TypeFilterLevel = TypeFilterLevel.Full;
            
            var channel = new IpcServerChannel(properties, serverProvider);
            ChannelServices.RegisterChannel(channel, false);
            return channel;
        }
    }
}
