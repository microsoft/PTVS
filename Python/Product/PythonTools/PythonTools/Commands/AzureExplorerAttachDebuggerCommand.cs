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

#if DEV12_OR_LATER && BUILDING_WITH_DEV12U3

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.PythonTools.Debugger.Remote;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Web.WindowsAzure.Contracts;
using Microsoft.VisualStudio.WindowsAzure.Authentication;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to attach to an Azure web site selected in Server Explorer.
    /// </summary>
    internal class AzureExplorerAttachDebuggerCommand : Command {
        public AzureExplorerAttachDebuggerCommand() {
            // Will throw PlatformNotSupportedException on any unsupported OS (Win7 and below).
            using (new ClientWebSocket()) { }

            try {
                ProbeWindowsAzureWebContractsAssembly();
            } catch (FileNotFoundException) {
                throw new NotSupportedException();
            } catch (FileLoadException) {
                throw new NotSupportedException();
            } catch (TypeLoadException) {
                throw new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Type ProbeWindowsAzureWebContractsAssembly() {
            return typeof(IVsAzureServices);
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

            Action<Task<bool>> onAttach = null;
            onAttach = (attachTask) => {
                if (!attachTask.Result) {
                    string msg = string.Format(
                        "Could not attach to python.exe process on Azure web site at {0}.\r\n\r\n" +
                        "Error retrieving websocket debug proxy information from web.config.",
                        webSite.Uri);
                    if (MessageBox.Show(msg, null, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Retry) {
                        AttachWorker(webSite).ContinueWith(onAttach);
                    }
                }
            };

            // We will need to do a bunch of async calls here, and they will deadlock if the UI thread is
            // blocked, so we can't use Wait() or Result, and have to let the task run on its own.
            AttachWorker(webSite).ContinueWith(onAttach);
        }

        /// <returns>
        /// Information about the current selected Azure web site node in Solution Explorer, or <c>null</c>
        /// if no node is selected, it's not a website node, or the information could not be retrieved.
        /// </returns>
        private AzureWebSiteInfo GetSelectedAzureWebSite() {
            // Get the current selected node in Solution Explorer.

            var shell = (IVsUIShell)PythonToolsPackage.GetGlobalService(typeof(SVsUIShell));
            var serverExplorerToolWindowGuid = new Guid(ToolWindowGuids.ServerExplorer);
            IVsWindowFrame serverExplorerFrame;
            shell.FindToolWindow(0, ref serverExplorerToolWindowGuid, out serverExplorerFrame);
            if (serverExplorerFrame == null) {
                return null;
            }

            object obj;
            serverExplorerFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out obj);
            var serverExplorerHierWnd = obj as IVsUIHierarchyWindow;
            if (serverExplorerHierWnd == null) {
                return null;
            }

            IntPtr hierPtr;
            uint itemid;
            IVsMultiItemSelect mis;
            serverExplorerHierWnd.GetCurrentSelection(out hierPtr, out itemid, out mis);
            if (hierPtr == IntPtr.Zero) {
                return null;
            }

            IVsHierarchy hier;
            try {
                hier = (IVsHierarchy)Marshal.GetObjectForIUnknown(hierPtr);
            } finally {
                Marshal.Release(hierPtr);
            }

            // Get the browse object of that node - this is the object that exposes properties to show in the Properties window.

            hier.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_SelContainer, out obj);
            var selCtr = obj as ISelectionContainer;
            if (selCtr == null) {
                return null;
            }

            var objs = new object[1];
            selCtr.GetObjects((uint)Microsoft.VisualStudio.Shell.Interop.Constants.GETOBJS_SELECTED, 1, objs);
            obj = objs[0];
            if (obj == null) {
                return null;
            }

            // We need to find out whether this is an Azure website object. We can't do a type check because the type of the 
            // browse object is private. We can, however, query for properties with specific names, and we can check the types
            // of those properties. In particular, WebSiteState is a public enum type that is a part of Azure Explorer public
            // contract, so we can check for it, and we can be reasonably sure that it is only exposed by web site nodes.

            var statusProp = obj.GetType().GetProperty("Status");
            if (statusProp == null ||
                statusProp.PropertyType.FullName != "Microsoft.VisualStudio.Web.WindowsAzure.Contracts.WebSiteState"
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

        private async Task<bool> AttachWorker(AzureWebSiteInfo webSite) {
            using (new WaitDialog("Azure remote debugging", "Attaching to Azure web site at " + webSite.Uri, PythonToolsPackage.Instance, showProgress: true)) {
                // Get path (relative to site URL) for the debugger endpoint.
                XDocument webConfig;
                try {
                    webConfig = await GetWebConfig(webSite);
                } catch (WebException) {
                    return false;
                } catch (IOException) {
                    return false;
                } catch (XmlException) {
                    return false;
                }
                if (webConfig == null) {
                    return false;
                }

                var path =
                    (from add in webConfig.Elements("configuration").Elements("system.webServer").Elements("handlers").Elements("add")
                     let type = (string)add.Attribute("type")
                     where type != null
                     let components = type.Split(',')
                     where components[0].Trim() == "Microsoft.PythonTools.Debugger.WebSocketProxy"
                     select (string)add.Attribute("path")
                    ).FirstOrDefault();
                if (path == null) {
                    return false;
                }

                var secret =
                    (from add in webConfig.Elements("configuration").Elements("appSettings").Elements("add")
                     where (string)add.Attribute("key") == "WSGI_PTVSD_SECRET"
                     select (string)add.Attribute("value")
                    ).FirstOrDefault();
                if (secret == null) {
                    return false;
                }

                try {
                    AttachDebugger(new UriBuilder(webSite.Uri) { Scheme = "wss", Port = -1, Path = path, UserName = secret }.Uri);
                } catch (Exception ex) {
                    // If we got to this point, the attach logic in debug engine will catch exceptions, display proper error message and
                    // ask the user to retry, so the only case where we actually get here is if user canceled on error. If this is the case,
                    // we don't want to pop any additional error messages, so always return true, but log the error in the Output window.
                    var output = OutputWindowRedirector.GetGeneral(PythonToolsPackage.Instance);
                    output.WriteErrorLine("Failed to attach to Azure web site: " + ex.Message);
                    output.ShowAndActivate();
                }
                return true;
            }
        }

        /// <summary>
        /// Retrieves web.config for a given Azure web site.
        /// </summary>
        /// <returns>XML document with the contents of web.config, or <c>null</c> if it could not be retrieved.</returns>
        private async Task<XDocument> GetWebConfig(AzureWebSiteInfo webSite) {
            var publishXml = await GetPublishXml(webSite);
            if (publishXml == null) {
                return null;
            }

            // Get FTP publish URL and credentials from publish settings.

            var publishProfile = publishXml.Elements("publishData").Elements("publishProfile").FirstOrDefault(el => (string)el.Attribute("publishMethod") == "FTP");
            if (publishProfile == null) {
                return null;
            }

            var publishUrl = (string)publishProfile.Attribute("publishUrl");
            var userName = (string)publishProfile.Attribute("userName");
            var userPwd = (string)publishProfile.Attribute("userPWD");
            if (publishUrl == null || userName == null || userPwd == null) {
                return null;
            }

            // Get web.config for the site via FTP.

            if (!publishUrl.EndsWith("/")) {
                publishUrl += "/";
            }
            publishUrl += "web.config";

            Uri webConfigUri;
            if (!Uri.TryCreate(publishUrl, UriKind.Absolute, out webConfigUri)) {
                return null;
            }

            var request = WebRequest.Create(webConfigUri) as FtpWebRequest;
            // Check that this is actually an FTP request, in case we get some valid but weird URL back.
            if (request == null) {
                return null;
            }
            request.Credentials = new NetworkCredential(userName, userPwd);

            using (var response = await request.GetResponseAsync())
            using (var stream = response.GetResponseStream()) {
                // There is no XDocument.LoadAsync, but we want the networked I/O at least to be async, even if parsing is not.
                var xmlData = new MemoryStream();
                await stream.CopyToAsync(xmlData);
                xmlData.Position = 0;
                return XDocument.Load(xmlData);
            }
        }

        /// <summary>
        /// Retrieves the publish settings file (.pubxml) for the given Azure web site.
        /// </summary>
        /// <returns>XML document with the contents of .pubxml, or <c>null</c> if it could not be retrieved.</returns>
        private async Task<XDocument> GetPublishXml(AzureWebSiteInfo webSiteInfo) {
            // To build the publish settings request URL, we need to know subscription ID, site name, and web region to which it belongs,
            // but we only have subscription ID and the public URL of the site at this point. Use the Azure web site service to look up
            // the site from those two, and retrieve the missing info.

            // The code below must avoid doing anything that would result in types from the Azure Tools contract assemblies being
            // referenced in any way in signatures of any members of any classes in this assembly. Because the contract assembly can
            // be be missing (if Azure SDK is not installed), such references will cause Reflection to break, which will break MEF
            // and block all our MEF exports. The main danger is classes generated by the compiler to support constructs such as
            // lambdas and await. To that extent, the following are not allowed in the code below:
            //
            //   - local variables of Azure types (become fields if captured by a lambda or used across await);
            //   - lambda arguments of Azure types (become arguments on generated method), and LINQ expressions that would implicitly
            //     produce such lambdas;
            //   - await on a Task that has result of an Azure type (produces a field of type TaskAwaiter<TResult>);
            //
            // To make it easier to verify, "var" and LINQ syntactic sugar should be avoided completely when dealing with Azure interfaces,
            // and all lambdas should have the types of arguments explicitly specified. For "await", cast the return type of any
            // Azure-type-returning async method to untyped Task first before awaiting, then use an explicit cast to Task<T> to read Result.
            // The only mentions of Azure types in the body of the method should be in casts.

            object webSiteServices = PythonToolsPackage.GetGlobalService(typeof(IVsAzureServices));
            if (webSiteServices == null) {
                return null;
            }

            object webSiteService = ((IVsAzureServices)webSiteServices).GetAzureWebSitesService();
            if (webSiteService == null) {
                return null;
            }

            Task getSubscriptionsAsyncTask = (Task)((IAzureWebSitesService)webSiteService).GetSubscriptionsAsync();
            await getSubscriptionsAsyncTask;

            IEnumerable<object> subscriptions = ((Task<List<IAzureSubscription>>)getSubscriptionsAsyncTask).Result;
            object subscription = subscriptions.FirstOrDefault((object sub) => ((IAzureSubscription)sub).SubscriptionId == webSiteInfo.SubscriptionId);
            if (subscription == null) {
                return null;
            }

            Task getResourcesAsyncTask = (Task)((IAzureSubscription)subscription).GetResourcesAsync(false);
            await getResourcesAsyncTask;

            IEnumerable<object> resources = ((Task<List<IAzureResource>>)getResourcesAsyncTask).Result;
            object webSite = resources.FirstOrDefault((object res) => {
                IAzureWebSite ws = res as IAzureWebSite;
                if (ws == null) {
                    return false;
                }
                Uri browseUri;
                Uri.TryCreate(ws.BrowseURL, UriKind.Absolute, out browseUri);
                return browseUri != null && browseUri.Equals(webSiteInfo.Uri);
            });
            if (webSite == null) {
                return null;
            }

            // Prepare a web request to get the publish settings.
            // See http://msdn.microsoft.com/en-us/library/windowsazure/dn166996.aspx
            string requestPath = string.Format(
                "{0}/services/WebSpaces/{1}/sites/{2}/publishxml",
                ((IAzureSubscription)subscription).SubscriptionId,
                ((IAzureWebSite)webSite).WebSpace,
                ((IAzureWebSite)webSite).Name);
            Uri requestUri = new Uri(((IAzureSubscription)subscription).ServiceManagementEndpointUri, requestPath);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
            request.Method = "GET";
            request.ContentType = "application/xml";
            request.Headers.Add("x-ms-version", "2010-10-28");

            // Set up authentication for the request, depending on whether the associated subscription context is 
            // account-based or certificate-based.
            object context = ((IAzureSubscription)subscription).AzureCredentials;
            if (context is IAzureAuthenticationCertificateSubscriptionContext) {
                X509Certificate2 cert = await ((IAzureAuthenticationCertificateSubscriptionContext)context).AuthenticationCertificate.GetCertificateFromStoreAsync();
                request.ClientCertificates.Add(cert);
            } else if (context is IAzureUserAccountSubscriptionContext) {
                string authHeader = await ((IAzureUserAccountSubscriptionContext)context).GetAuthenticationHeaderAsync(false);
                request.Headers.Add(HttpRequestHeader.Authorization, authHeader);
            } else {
                return null;
            }

            using (WebResponse response = await request.GetResponseAsync())
            using (Stream stream = response.GetResponseStream()) {
                // There is no XDocument.LoadAsync, but we want the networked I/O at least to be async, even if parsing is not.
                Stream xmlData = new MemoryStream();
                await stream.CopyToAsync(xmlData);
                xmlData.Position = 0;
                return XDocument.Load(xmlData);
            }
        }

        private unsafe void AttachDebugger(Uri uri) {
            var dte = (EnvDTE.DTE)PythonToolsPackage.GetGlobalService(typeof(EnvDTE.DTE));
            var debugger = (EnvDTE90.Debugger3)dte.Debugger;

            var transports = debugger.Transports;
            EnvDTE80.Transport transport = null;
            for (int i = 1; i <= transports.Count; ++i) {
                var t = transports.Item(i);
                Guid tid;
                if (Guid.TryParse(t.ID, out tid) && tid == PythonRemoteDebugPortSupplier.PortSupplierGuid) {
                    transport = t;
                }
            }
            if (transport == null) {
                throw new InvalidOperationException("Python remote debugging transport is missing.");
            }

            var processes = debugger.GetProcesses(transport, uri.ToString());
            if (processes.Count == 0) {
                throw new InvalidOperationException("No Python processes found on remote host.");
            }

            foreach (EnvDTE.Process process in processes) {
                process.Attach();
            }
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

#endif