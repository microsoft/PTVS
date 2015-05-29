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
