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
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Repl
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Description("Visual Studio REPL Window")]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideKeyBindingTable(ReplWindow.TypeGuid, 200)]        // Resource ID: "Interactive Console"
    [ProvideAutoLoad(CommonConstants.UIContextNoSolution)]
    [ProvideAutoLoad(CommonConstants.UIContextSolutionExists)]
    [ProvideToolWindow(typeof(ReplWindow), MultiInstances = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidReplWindowPkgString)]
    internal sealed class ReplWindowPackage : Package, IVsToolWindowFactory
    {
        internal static ReplWindowPackage Instance;
        
        public ReplWindowPackage() {
            Instance = this;
        }

        int IVsToolWindowFactory.CreateToolWindow(ref Guid toolWindowType, uint id) {
            int num = (int)id;
            if (toolWindowType == typeof(ReplWindow).GUID) {
                string evalAsm, eval, contentType, title, replId;
                Guid langSvcGuid;
                if (GetReplInfo(num, out evalAsm, out eval, out contentType, out title, out langSvcGuid, out replId)) {

                    var model = (IComponentModel)GetService(typeof(SComponentModel));
                    var contentTypes = model.GetService<IContentTypeRegistryService>();
                    var contentTypeObj = contentTypes.GetContentType(contentType);
                    var evaluator = GetReplEvaluator(model, replId);
                    if (evaluator != null) {
                        var replProvider = model.GetExtensions<IReplWindowProvider>().First();
                        ((ReplWindowProvider)replProvider).CreateReplWindow(evaluator, contentTypeObj, num, title, langSvcGuid, replId);

                        return VSConstants.S_OK;
                    }
                }

                return VSConstants.E_FAIL;
            }

            return VSConstants.E_FAIL;
        }

        private static IReplEvaluator GetReplEvaluator(IComponentModel model, string replId) {
            foreach (var provider in model.GetExtensions<IReplEvaluatorProvider>()) {
                var evaluator = provider.GetEvaluator(replId);

                if (evaluator != null) {
                    return evaluator;
                }
            }
            return null;
        }

        const string ActiveReplsKey = "ActiveRepls";

        public void SaveReplInfo(int id, IReplEvaluator evaluator, IContentType contentType, string title, Guid languageServiceGuid, string replId) {
            using (var repl = UserRegistryRoot.CreateSubKey(ActiveReplsKey)) {
                using (var curRepl = repl.CreateSubKey(id.ToString())) {
                    curRepl.SetValue("EvaluatorType", evaluator.GetType().FullName);
                    curRepl.SetValue("EvaluatorAssembly", evaluator.GetType().Assembly.FullName);
                    curRepl.SetValue("ContentType", contentType.TypeName);
                    curRepl.SetValue("Title", title);
                    curRepl.SetValue("ReplId", replId.ToString());
                    curRepl.SetValue("LanguageServiceGuid", languageServiceGuid.ToString());
                }
            }
        }

        public bool GetReplInfo(int id, out string evaluator, out string evalType, out string contentType, out string title, out Guid languageServiceGuid, out string replId) {
            using (var repl = UserRegistryRoot.OpenSubKey(ActiveReplsKey)) {
                if (repl != null) {
                    using (var curRepl = repl.OpenSubKey(id.ToString())) {
                        if (curRepl != null) {
                            evaluator = (string)curRepl.GetValue("EvaluatorAssembly");
                            evalType = (string)curRepl.GetValue("EvaluatorType");
                            contentType = (string)curRepl.GetValue("ContentType");
                            title = (string)curRepl.GetValue("Title");
                            replId = curRepl.GetValue("ReplId").ToString();
                            languageServiceGuid = Guid.Parse((string)curRepl.GetValue("LanguageServiceGuid"));
                            return true;
                        }
                    }
                }
            }
            evaluator = null;
            contentType = null;
            title = null;
            evalType = null;
            replId = "";
            languageServiceGuid = Guid.Empty;
            return false;
        }
    }
}
