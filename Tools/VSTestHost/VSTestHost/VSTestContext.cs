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
        private static bool _isMock;

        public static IServiceProvider ServiceProvider {
            get {
                if (_serviceProvider == null) {
                    throw new InvalidOperationException(Internal.Resources.NoServiceProvider);
                }
                return _serviceProvider;
            }
            set {
                _serviceProvider = value;
                if (_serviceProvider != null) {
                    _dte = (EnvDTE.DTE)_serviceProvider.GetService(typeof(EnvDTE.DTE));
                } else {
                    _dte = null;
                }
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

        public static bool IsMock {
            get {
                return _isMock;
            }
            internal set {
                _isMock = value;
            }
        }
    }
}

#endif
