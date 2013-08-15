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
