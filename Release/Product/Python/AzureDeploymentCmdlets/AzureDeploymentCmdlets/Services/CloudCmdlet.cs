// ----------------------------------------------------------------------------------
//
// Copyright 2011 Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{
    using System;
    using System.Globalization;
    using System.Management.Automation;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Security;
    using System.ServiceModel.Web;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
    using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;

    public abstract class CloudCmdlet<T> : CmdletBase
    {
        protected X509Certificate2 certificate;
        protected string subscriptionId;

        /// <summary>
        /// Initializes a new instance of the CloudCmdlet class.
        /// </summary>
        public CloudCmdlet()
        {
            GlobalComponents globalComponents = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory);
            certificate = globalComponents.Certificate;
        }

        public Binding ServiceBinding
        {
            get;
            set;
        }

        public string ServiceEndpoint
        {
            get;
            set;
        }

        public int MaxStringContentLength
        {
            get;
            set;
        }

        protected T Channel
        {
            get;
            set;
        }

        protected static string RetrieveOperationId()
        {
            var operationId = string.Empty;

            if ((WebOperationContext.Current != null) && (WebOperationContext.Current.IncomingResponse != null))
            {
                operationId = WebOperationContext.Current.IncomingResponse.Headers[Constants.OperationTrackingIdHeader];
            }

            return operationId;
        }

        protected virtual void WriteErrorDetails(CommunicationException exception)
        {
            ServiceManagementError error = null;
            ServiceManagementHelper.TryGetExceptionDetails(exception, out error);

            if (error == null)
            {
                SafeWriteError(new ErrorRecord(exception, string.Empty, ErrorCategory.CloseError, null));
            }
            else
            {
                string errorDetails = string.Format(
                    CultureInfo.InvariantCulture,
                    "HTTP Status Code: {0} - HTTP Error Message: {1}",
                    error.Code,
                    error.Message);

                SafeWriteError(new ErrorRecord(new CommunicationException(errorDetails), string.Empty, ErrorCategory.CloseError, null));
            }
        }

        protected override void ProcessRecord()
        {
            Validate.ValidateInternetConnection();
            base.ProcessRecord();

            if (this.Channel == null)
            {
                this.Channel = this.CreateChannel();
            }
        }

        protected abstract T CreateChannel();

        protected void RetryCall(Action<string> call)
        {
            this.RetryCall(this.subscriptionId, call);
        }

        protected void RetryCall(string subscriptionId, Action<string> call)
        {
            try
            {
                try
                {
                    call(subscriptionId);
                }
                catch (MessageSecurityException ex)
                {
                    var webException = ex.InnerException as WebException;

                    if (webException == null)
                    {
                        throw;
                    }

                    var webResponse = webException.Response as HttpWebResponse;

                    if (webResponse != null && webResponse.StatusCode == HttpStatusCode.Forbidden)
                    {
                        this.Channel = this.CreateChannel();
                        if (subscriptionId.Equals(subscriptionId.ToUpper(CultureInfo.InvariantCulture)))
                        {
                            call(subscriptionId.ToLower(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            call(subscriptionId.ToUpper(CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            catch (MessageSecurityException ex)
            {
                var webException = ex.InnerException as WebException;

                if (webException == null)
                {
                    throw;
                }

                var webResponse = webException.Response as HttpWebResponse;

                if (webResponse != null && webResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    this.Channel = this.CreateChannel();
                    if (subscriptionId.Equals(subscriptionId.ToUpper(CultureInfo.InvariantCulture)))
                    {
                        call(subscriptionId.ToLower(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        call(subscriptionId.ToUpper(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        protected TResult RetryCall<TResult>(Func<string, TResult> call)
        {
            return this.RetryCall(this.subscriptionId, call);
        }

        protected TResult RetryCall<TResult>(string subscriptionId, Func<string, TResult> call)
        {
            try
            {
                try
                {
                    return call(subscriptionId);
                }
                catch (MessageSecurityException ex)
                {
                    var webException = ex.InnerException as WebException;

                    if (webException == null)
                    {
                        throw;
                    }

                    var webResponse = webException.Response as HttpWebResponse;

                    if (webResponse != null && webResponse.StatusCode == HttpStatusCode.Forbidden)
                    {
                        if (subscriptionId.Equals(subscriptionId.ToUpper(CultureInfo.InvariantCulture)))
                        {
                            return call(subscriptionId.ToLower(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            return call(subscriptionId.ToUpper(CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            catch (MessageSecurityException ex)
            {
                var webException = ex.InnerException as WebException;

                if (webException == null)
                {
                    throw;
                }

                var webResponse = webException.Response as HttpWebResponse;

                if (webResponse != null && webResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    if (subscriptionId.Equals(subscriptionId.ToUpper(CultureInfo.InvariantCulture)))
                    {
                        return call(subscriptionId.ToLower(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        return call(subscriptionId.ToUpper(CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        protected ServiceSettings GetDefaultSettings(string rootPath, string inServiceName, string slot, string location, string storageName, string subscription, out string serviceName)
        {
            ServiceSettings serviceSettings;

            if (string.IsNullOrEmpty(rootPath))
            {
                serviceSettings = ServiceSettings.LoadDefault(null, slot, location, subscription, storageName, inServiceName, null, out serviceName);
            }
            else
            {
                serviceSettings = ServiceSettings.LoadDefault(new AzureService(rootPath, null).Paths.Settings,
                slot, location, subscription, storageName, inServiceName, new AzureService(rootPath, null).ServiceName, out serviceName);
            }

            return serviceSettings;
        }

        /// <summary>
        /// Invoke the given operation within an OperationContextScope if the
        /// channel supports it.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        protected void InvokeInOperationContext(Action action)
        {
            IContextChannel contextChannel = Channel as IContextChannel;
            if (contextChannel != null)
            {
                using (new OperationContextScope(contextChannel))
                {
                    action();
                }
            }
            else
            {
                action();
            }
        }
    }
}