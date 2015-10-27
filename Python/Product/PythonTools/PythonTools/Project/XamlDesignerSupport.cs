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
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Designer;
using Microsoft.Windows.Design.Host;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Provides access to the DesignerContext and WpfEventBindingProvider assuming that functionality
    /// is installed into VS.  If it's not installed then this becomes a nop and DesignerContextType
    /// returns null;
    /// </summary>
    class XamlDesignerSupport {
        private static readonly Type _designerContextType;

        static XamlDesignerSupport() {
            try {
                _designerContextType = GetDesignerContextType();
            } catch (FileNotFoundException) {
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Type GetDesignerContextType() {
            return typeof(DesignerContext);
        }

        public static object CreateDesignerContext() {
            if (_designerContextType != null) {
                return CreateDesignerContextNoInline();
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object CreateDesignerContextNoInline() {
            var res = new DesignerContext();
            //Set the RuntimeNameProvider so the XAML designer will call it when items are added to
            //a design surface. Since the provider does not depend on an item context, we provide it at 
            //the project level.
            // This is currently disabled because we don't successfully serialize to the remote domain
            // and the default name provider seems to work fine.  Likely installing our assembly into
            // the GAC or implementing an IsolationProvider would solve this.
            //res.RuntimeNameProvider = new PythonRuntimeNameProvider();
            return res;
        }

        public static void InitializeEventBindingProvider(object designerContext, PythonFileNode codeNode) {
            if (_designerContextType != null) {
                InitializeEventBindingProviderNoInline(designerContext, codeNode);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitializeEventBindingProviderNoInline(object designerContext, PythonFileNode codeNode) {
            Debug.Assert(designerContext is DesignerContext);
            ((DesignerContext)designerContext).EventBindingProvider = new WpfEventBindingProvider(codeNode as PythonFileNode);
        }

        public static Type DesignerContextType {
            get {
                return _designerContextType;
            }
        }
    }
}
