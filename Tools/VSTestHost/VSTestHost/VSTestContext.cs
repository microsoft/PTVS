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

#if SUPPORT_TESTEE

using System;

namespace Microsoft.VisualStudioTools.VSTestHost {
    public static class VSTestContext {
        private static IServiceProvider _serviceProvider;
        private static EnvDTE.DTE _dte;

        internal static void SetServiceProvider(IServiceProvider provider) {
            if (_serviceProvider != null) {
                throw new InvalidOperationException(Internal.Resources.ServiceProviderAlreadySet);
            }

            _serviceProvider = provider;
            _dte = (EnvDTE.DTE)provider.GetService(typeof(EnvDTE.DTE));
        }

        public static IServiceProvider ServiceProvider {
            get {
                if (_serviceProvider == null) {
                    throw new InvalidOperationException(Internal.Resources.NoServiceProvider);
                }
                return _serviceProvider;
            }
        }

        public static EnvDTE.DTE DTE {
            get {
                if (_dte == null) {
                    throw new InvalidOperationException(Internal.Resources.NoServiceProvider);
                }
                return _dte;
            }
        }
    }
}

#endif
