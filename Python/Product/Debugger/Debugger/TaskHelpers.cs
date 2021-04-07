// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.
extern alias Interop10;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using static System.FormattableString;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Debugger {
    static class TaskHelpers {
        private static readonly Lazy<IVsThreadedWaitDialogFactory> _twdf = new Lazy<IVsThreadedWaitDialogFactory>(() => {
            return (IVsThreadedWaitDialogFactory)Package.GetGlobalService(typeof(SVsThreadedWaitDialogFactory));
        });

        public static void RunSynchronouslyOnUIThread(Func<CancellationToken, Task> method, double delayToShowDialog = 2) {
            ThreadHelper.ThrowIfNotOnUIThread();

            using (var session = StartWaitDialog(delayToShowDialog)) {
                var ct = session?.UserCancellationToken ?? default(CancellationToken);
                ThreadHelper.JoinableTaskFactory.Run(() => method(ct));
            }
        }

        public static T RunSynchronouslyOnUIThread<T>(Func<CancellationToken, Task<T>> method, double delayToShowDialog = 2) {
            T result;
            using (var session = StartWaitDialog(delayToShowDialog)) {
                var ct = session?.UserCancellationToken ?? default(CancellationToken);
                result = ThreadHelper.JoinableTaskFactory.Run(() => method(ct));
            }

            return result;
        }

        private static ThreadedWaitDialogHelper.Session StartWaitDialog(double delayToShowDialog) {
            var factory = _twdf.Value as Interop10::Microsoft.VisualStudio.Shell.Interop.IVsThreadedWaitDialogFactory;
            return factory?.StartWaitDialog(
                null,
                new ThreadedWaitDialogProgressData(Strings.DebuggerInProgress, isCancelable: true),
                TimeSpan.FromSeconds(delayToShowDialog)
            );
        }
    }
}
