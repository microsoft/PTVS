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
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to attach to an Azure web site selected in Server Explorer.
    /// </summary>
    internal class AzureExplorerAttachDebuggerCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public AzureExplorerAttachDebuggerCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidAzureExplorerAttachPythonDebugger; }
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    var oleMenuCmd = (Microsoft.VisualStudio.Shell.OleMenuCommand)sender;
                    oleMenuCmd.Supported = oleMenuCmd.Visible = (GetSelectedAzureWebSite() != null);
                };
            }
        }

        public override void DoCommand(object sender, EventArgs args) {
            var webSite = GetSelectedAzureWebSite();
            if (webSite == null) {
                throw new NotSupportedException();
            }

            Uri debugUri;
            if (Uri.TryCreate(webSite.Uri, "/ptvsd", out debugUri)) {
                // Open the site's ptvsd page if it exists
                var req = WebRequest.CreateHttp(debugUri.AbsoluteUri);
                req.Method = "HEAD";
                req.Accept = "text/html";

                var dlgFactory = (IVsThreadedWaitDialogFactory)_serviceProvider.GetService(typeof(SVsThreadedWaitDialogFactory));
                IVsThreadedWaitDialog2 dlg = null;
                if (dlgFactory != null && ErrorHandler.Succeeded(dlgFactory.CreateInstance(out dlg))) {
                    if (ErrorHandler.Failed(dlg.StartWaitDialog(
                        Strings.ProductTitle,
                        Strings.DebugAttachGettingSiteInformation,
                        null,
                        null,
                        null,
                        1,
                        false,
                        true
                    ))) {
                        dlg = null;
                    }
                }
                try {
                    req.GetResponse().Close();
                } catch (WebException) {
                    debugUri = null;
                } finally {
                    if (dlg != null) {
                        int dummy;
                        dlg.EndWaitDialog(out dummy);
                    }
                }
            }

            if (debugUri != null) {
                CommonPackage.OpenWebBrowser(_serviceProvider, debugUri.AbsoluteUri);
            } else {
                CommonPackage.OpenWebBrowser(_serviceProvider, "https://go.microsoft.com/fwlink/?LinkID=624026");
            }
        }

        /// <returns>
        /// Information about the current selected Azure web site node in Solution Explorer, or <c>null</c>
        /// if no node is selected, it's not a website node, or the information could not be retrieved.
        /// </returns>
        private AzureWebSiteInfo GetSelectedAzureWebSite() {
            // Get the current selected node in Solution Explorer.

            var shell = (IVsUIShell)_serviceProvider.GetService(typeof(SVsUIShell));
            var serverExplorerToolWindowGuid = new Guid(ToolWindowGuids.ServerExplorer);
            IVsWindowFrame serverExplorerFrame;
            if (ErrorHandler.Failed(shell.FindToolWindow(0, ref serverExplorerToolWindowGuid, out serverExplorerFrame)) ||
                serverExplorerFrame == null) {
                return null;
            }

            object obj;
            if (ErrorHandler.Failed(serverExplorerFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out obj))) {
                return null;
            }
            var serverExplorerHierWnd = obj as IVsUIHierarchyWindow;
            if (serverExplorerHierWnd == null) {
                return null;
            }

            IntPtr hierPtr;
            uint itemid;
            IVsMultiItemSelect mis;
            if (ErrorHandler.Failed(serverExplorerHierWnd.GetCurrentSelection(out hierPtr, out itemid, out mis)) ||
                hierPtr == IntPtr.Zero) {
                return null;
            }

            IVsHierarchy hier;
            try {
                hier = (IVsHierarchy)Marshal.GetObjectForIUnknown(hierPtr);
            } finally {
                Marshal.Release(hierPtr);
            }

            // Get the browse object of that node - this is the object that exposes properties to show in the Properties window.

            if (ErrorHandler.Failed(hier.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_SelContainer, out obj))) {
                return null;
            }
            var selCtr = obj as ISelectionContainer;
            if (selCtr == null) {
                return null;
            }

            var objs = new object[1];
            if (ErrorHandler.Failed(selCtr.GetObjects((uint)Constants.GETOBJS_SELECTED, 1, objs))) {
                return null;
            }
            obj = objs[0];
            if (obj == null) {
                return null;
            }

            // We need to find out whether this is an Azure Website object. We can't do a type check because the type of the 
            // browse object is private. We can, however, query for properties with specific names, and we can check the types
            // of those properties. In particular, WebSiteState is a public enum type that is a part of the Azure Explorer contract, 
            // so we can check for it, and we can be reasonably sure that it is only exposed by website nodes. It seems that
            // the namespace is subject to change, however, as it was marked internal in VS2015.

            var statusProp = obj.GetType().GetProperty("Status");
            if (statusProp == null ||
                (statusProp.PropertyType.FullName != "Microsoft.VisualStudio.Web.WindowsAzure.Contracts.WebSiteState" &&
                statusProp.PropertyType.FullName != "Microsoft.VisualStudio.Web.Internal.Contracts.WebSiteState")
            ) {
                return null;
            }

            // Is the web site running?
            int status = (int)statusProp.GetValue(obj);
            if (status != 1) {
                return null;
            }

            // Get the URI
            var urlProp = obj.GetType().GetProperty("Url");
            if (urlProp == null || urlProp.PropertyType != typeof(string)) {
                return null;
            }
            Uri uri;
            if (!Uri.TryCreate((string)urlProp.GetValue(obj), UriKind.Absolute, out uri)) {
                return null;
            }

            // Get Azure subscription ID
            var subIdProp = obj.GetType().GetProperty("SubscriptionID");
            if (subIdProp == null || subIdProp.PropertyType != typeof(string)) {
                return null;
            }
            string subscriptionId = (string)subIdProp.GetValue(obj);

            return new AzureWebSiteInfo(uri, subscriptionId);
        }

        /// <summary>
        /// Information about an Azure Web Site node in Server Explorer.
        /// </summary>
        private class AzureWebSiteInfo {
            public readonly Uri Uri;
            public readonly string SubscriptionId;

            public AzureWebSiteInfo(Uri uri, string subscriptionId) {
                Uri = uri;
                SubscriptionId = subscriptionId;
            }
        }
    }
}
