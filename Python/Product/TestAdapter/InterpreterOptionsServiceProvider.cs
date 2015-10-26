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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.PythonTools {
    static class InterpreterOptionsServiceProvider {
        public static IInterpreterOptionsService GetService(VisualStudioApp app) {
            var provider = new MockExportProvider();
            var container = new CompositionContainer(
                new AssemblyCatalog(typeof(IInterpreterOptionsService).Assembly),
                provider
            );
            if (app != null) {
                var sp = new ServiceProvider(app.GetDTE() as IOleServiceProvider);
                provider.SetExport(typeof(SVsServiceProvider), () => sp);
            }
            return container.GetExportedValue<IInterpreterOptionsService>();
        }
    }
}
