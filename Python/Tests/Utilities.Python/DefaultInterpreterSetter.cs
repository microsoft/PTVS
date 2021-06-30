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

using System;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI {
    public class DefaultInterpreterSetter : IDisposable {
        private readonly IComponentModel _model;
        public readonly IPythonInterpreterFactory OriginalInterpreter;
        private bool _isDisposed;

        public DefaultInterpreterSetter(IPythonInterpreterFactory factory, IServiceProvider site) {
            Assert.IsNotNull(factory, "Cannot set default to null");
            _model = (IComponentModel)(site).GetService(typeof(SComponentModel));
            var interpreterService = _model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(interpreterService);

            OriginalInterpreter = interpreterService.DefaultInterpreter;
            CurrentDefault = factory;
            interpreterService.DefaultInterpreter = factory;
        }

        public void SetDefault(IPythonInterpreterFactory factory) {
            Assert.IsNotNull(factory, "Cannot set default to null");
            var interpreterService = _model.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(interpreterService);

            CurrentDefault = factory;
            interpreterService.DefaultInterpreter = factory;
        }

        public IPythonInterpreterFactory CurrentDefault { get; private set; }


        public void Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;

                var interpreterService = _model.GetService<IInterpreterOptionsService>();
                Assert.IsNotNull(interpreterService);
                interpreterService.DefaultInterpreter = OriginalInterpreter;
            }
        }
    }
}
