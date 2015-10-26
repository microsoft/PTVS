// Visual Studio Shared Project
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
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using System.Xaml;
using System.Windows.Data;
using System.Globalization;
using Microsoft.VisualStudioTools.Wpf;

namespace Microsoft.VisualStudioTools.Wpf {
    [ContentProperty("Lambda")]
    public class LambdaExtension : MarkupExtension {
        public string Lambda { get; set; }

        public LambdaExtension() {
        }

        public LambdaExtension(string lambda) {
            Lambda = lambda;
        }

        public override object ProvideValue(IServiceProvider serviceProvider) {
            if (Lambda == null) {
                throw new InvalidOperationException("Lambda not specified");
            }

            var rootProvider = (IRootObjectProvider)serviceProvider.GetService(typeof(IRootObjectProvider));
            var root = rootProvider.RootObject;
            if (root == null) {
                throw new InvalidOperationException("Cannot locate root object - service provider did not provide IRootObjectProvider");
            }

            var provider = root as ILambdaConverterProvider;
            if (provider == null) {
                throw new InvalidOperationException("Root object does not implement ILambdaConverterProvider - code generator not run");
            }

            return provider.GetConverterForLambda(Lambda);
        }
    }
}
