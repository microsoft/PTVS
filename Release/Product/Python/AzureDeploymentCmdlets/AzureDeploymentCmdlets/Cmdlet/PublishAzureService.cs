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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using System.ServiceModel;
using System.Threading;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;
using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Cmdlet
{
    /// <summary>
    /// Create a new deployment. Note that there shouldn't be a deployment 
    /// of the same name or in the same slot when executing this command.
    /// </summary>
    [Cmdlet(VerbsData.Publish, "AzureService")]
    public class PublishAzureServiceCommand : ServiceManagementCmdletBase
    {
        private DeploymentSettings _deploymentSettings;
        private AzureService _azureService;
        private string _hostedServiceName;

        [Parameter(Mandatory = false)]
        [Alias("sn")]
        public string Subscription { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("sv")]
        public string Name { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("st")]
        public string StorageAccountName { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("l")]
        public string Location { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("sl")]
        public string Slot { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("ln")]
        public SwitchParameter Launch { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether CreateChannel should share
        /// the command's current Channel when asking for a new one.  This is
        /// only used for testing.
        /// </summary>
        internal bool ShareChannel { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether publishing should skip the
        /// upload step.  This is only used for testing.
        /// </summary>
        internal bool SkipUpload { get; set; }

        /// <summary>
        /// Initializes a new instance of the PublishAzureServiceCommand class.
        /// </summary>
        public PublishAzureServiceCommand()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PublishAzureServiceCommand class.
        /// </summary>
        /// <param name="channel">
        /// Channel used for communication with Azure's service management APIs.
        /// </param>
        public PublishAzureServiceCommand(IServiceManagement channel)
        {
            Channel = channel;
        }

        /// <summary>
        /// Execute the command.
        /// </summary>
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        protected override void ProcessRecord()
        {
            try
            {
                base.ProcessRecord();

                PublishService(GetServiceRootPath());

                SafeWriteObjectWithTimestamp(Resources.PublishCompleteMessage);

            }
            catch (Exception ex)
            {
                SafeWriteError(ex);
            }
        }

        /// <summary>
        /// Publish an Azure service defined at the given path. Publish flow is as following:
        /// * Checks is service is available.
        ///     * Exists:
        ///         * Checks if deployment slot is available.
        ///             * Exists: update the deployment with new package.
        ///             * Does not exist: create new deployment for this slot.
        ///     * Does not exist:
        ///         1. Create new service.
        ///         2. Create new deployment for slot.
        /// * Verifies the deployment until its published.
        /// </summary>
        /// <param name="serviceRootPath">
        /// Path where the service exists.
        /// </param>
        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        public void PublishService(string serviceRootPath)
        {
            SafeWriteObject(string.Format(Resources.PublishServiceStartMessage, _hostedServiceName));
            SafeWriteObject(string.Empty);

            // Package the service and all of its roles up in the open package format used by Azure
            InitializeSettingsAndCreatePackage(serviceRootPath);

            if (ServiceExists())
            {
                bool deploymentExists = new GetDeploymentStatus(this.Channel).DeploymentExists(_azureService.Paths.RootPath, _hostedServiceName, _deploymentSettings.ServiceSettings.Slot, _deploymentSettings.ServiceSettings.Subscription);

                if (deploymentExists)
                {

                    UpgradeDeployment();
                }
                else
                {
                    CreateNewDeployment();
                }
            }
            else
            {
                CreateHostedService();
                CreateNewDeployment();
            }

            // Verify the deployment succeeded by checking that each of the
            // roles are running
            VerifyDeployment();

            // After we've finished deploying, optionally launch a browser pointed at the service
            if (Launch && CanGenerateUrlForDeploymentSlot())
            {
                LaunchService();
            }
        }

        /// <summary>
        /// Gets a value indicating whether we'll be able to automatically get
        /// the deployment URL for the given deployment slot.
        /// </summary>
        /// <returns>
        /// A value indicating whether we'll be able to automatically get
        /// the deployed URL for the given deployment slot.
        /// </returns>
        private bool CanGenerateUrlForDeploymentSlot()
        {
            return string.Equals(
                _deploymentSettings.ServiceSettings.Slot,
                ArgumentConstants.Slots[AzureDeploymentCmdlets.Model.Slot.Production]);
        }

        /// <summary>
        /// Initialize our model of the AzureService located at the given
        /// path along with its DeploymentSettings and SubscriptionId.
        /// </summary>
        /// <param name="rootPath">Root path of the Azure service.</param>
        internal void InitializeSettingsAndCreatePackage(string rootPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(rootPath), "rootPath cannot be null or empty.");
            Debug.Assert(Directory.Exists(rootPath), "rootPath does not exist.");

            _azureService = new AzureService(rootPath, null);

            // If user provided a service name, change current service name to use it.
            //
            if (!string.IsNullOrEmpty(Name))
            {
                _azureService.ChangeServiceName(Name, _azureService.Paths);
            }

            ServiceSettings defaultSettings = ServiceSettings.LoadDefault(
                _azureService.Paths.Settings,
                Slot,
                Location,
                Subscription,
                StorageAccountName,
                Name,
                _azureService.ServiceName,
                out _hostedServiceName);

            subscriptionId =
                new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory)
                .GetSubscriptionId(defaultSettings.Subscription);

            SafeWriteObjectWithTimestamp(String.Format(Resources.PublishPreparingDeploymentMessage,
                _hostedServiceName, subscriptionId));

            CreatePackage();

            _deploymentSettings = new DeploymentSettings(
                defaultSettings,
                _azureService.Paths.CloudPackage,
                _azureService.Paths.CloudConfiguration,
                _hostedServiceName,
                string.Format(Resources.ServiceDeploymentName, defaultSettings.Slot));

        }

        /// <summary>
        /// Create the package that we'll upload to Azure blob storage before
        /// deploying.
        /// </summary>
        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        private void CreatePackage()
        {
            Debug.Assert(_azureService != null);

            string unused = null;
            _azureService.CreatePackage(DevEnv.Cloud, out unused, out unused);
        }

        /// <summary>
        /// Determine if a service already exists.
        /// </summary>
        /// <returns>
        /// A value indicating whether the service already exists.
        /// </returns>
        /// <remarks>
        /// Service names are unique across Azure.
        /// </remarks>
        private bool ServiceExists()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_hostedServiceName),
                "_hostedServiceName cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(subscriptionId),
                "subscriptionId cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.Slot),
                "Slot cannot be null.");

            SafeWriteObjectWithTimestamp(Resources.PublishConnectingMessage);

            // Check whether there's an existing service with the desired
            // name in the desired slot accessible from our subscription

            try
            {
                HostedService existingService = RetryCall(subscription => Channel.GetHostedServiceWithDetails(subscription, _hostedServiceName, true));
            }
            catch (EndpointNotFoundException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Create a hosted Azure service.
        /// </summary>
        private void CreateHostedService()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_hostedServiceName),
                "_hostedServiceName cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.Label),
                "Label cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.Location),
                "Location cannot be null or empty.");

            SafeWriteObjectWithTimestamp(Resources.PublishCreatingServiceMessage);

            CreateHostedServiceInput hostedServiceInput = new CreateHostedServiceInput
            {
                ServiceName = _hostedServiceName,
                Label = ServiceManagementHelper.EncodeToBase64String(_deploymentSettings.Label),
                Location = _deploymentSettings.ServiceSettings.Location
            };

            InvokeInOperationContext(() =>
            {
                RetryCall(subscription =>
                    Channel.CreateHostedService(subscription, hostedServiceInput));
                SafeWriteObjectWithTimestamp(String.Format(Resources.PublishCreatedServiceMessage,
                    hostedServiceInput.ServiceName));
            });
        }

        /// <summary>
        /// Upload the package to our blob storage account.
        /// </summary>
        /// <returns>Uri to the uploaded package.</returns>
        private Uri UploadPackage()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_azureService.Paths.CloudPackage),
                "CloudPackage cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.StorageAccountName),
                "StorageAccountName cannot be null or empty.");

            string packagePath = _azureService.Paths.CloudPackage;
            Validate.ValidateFileFull(packagePath, Resources.Package);

            Uri packageUri = null;
            if (packagePath.StartsWith(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                packagePath.StartsWith(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                packageUri = new Uri(packagePath);
            }
            else
            {
                SafeWriteObjectWithTimestamp(String.Format(Resources.PublishVerifyingStorageMessage,
                _deploymentSettings.ServiceSettings.StorageAccountName));

                if (!StorageAccountExists())
                {
                    CreateStorageAccount();
                }

                SafeWriteObjectWithTimestamp(Resources.PublishUploadingPackageMessage);

                if (!SkipUpload)
                {
                    packageUri = RetryCall(subscription =>
                        AzureBlob.UploadPackageToBlob(
                            CreateChannel(),
                            _deploymentSettings.ServiceSettings.StorageAccountName,
                            subscription,
                            ResolvePath(packagePath)));
                }
                else
                {
                    packageUri = new Uri(packagePath);
                }
            }
            return packageUri;
        }

        /// <summary>
        /// Removes the package after uploading it to the storage account
        /// </summary>
        private void RemovePackage()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.StorageAccountName),
                "StorageAccountName cannot be null or empty.");

            if (!SkipUpload)
            {

                RetryCall(subscription =>
                        AzureBlob.RemovePackageFromBlob(
                            CreateChannel(),
                            _deploymentSettings.ServiceSettings.StorageAccountName,
                            subscription));
            }
        }

        /// <summary>
        /// Check if the service's storage account already exists.
        /// </summary>
        /// <returns>
        /// A value indicating whether the service's storage account already
        /// exists.
        /// </returns>
        private bool StorageAccountExists()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.StorageAccountName),
                "StorageAccountName cannot be null or empty.");

            StorageService storageService = null;
            try
            {
                storageService = RetryCall(subscription =>
                    Channel.GetStorageService(
                        subscription,
                        _deploymentSettings.ServiceSettings.StorageAccountName));
            }
            catch (EndpointNotFoundException)
            {
                // Don't write error message.  This catch block is used to
                // detect that there's no such endpoint which indicates that
                // the storage account doesn't exist.
                return false;
            }

            return storageService != null;
        }

        /// <summary>
        /// Create an Azure storage account that we can use to upload our
        /// package when creating and deploying a service.
        /// </summary>
        private void CreateStorageAccount()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.StorageAccountName),
                "StorageAccountName cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.Label),
                "Label cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.Location),
                "Location cannot be null or empty.");

            CreateStorageServiceInput storageServiceInput = new CreateStorageServiceInput
            {
                ServiceName = _deploymentSettings.ServiceSettings.StorageAccountName,
                Label = ServiceManagementHelper.EncodeToBase64String(_deploymentSettings.Label),
                Location = _deploymentSettings.ServiceSettings.Location
            };

            InvokeInOperationContext(() =>
            {
                RetryCall(subscription =>
                    Channel.CreateStorageAccount(subscription, storageServiceInput));

                StorageService storageService = null;
                do
                {
                    storageService = RetryCall(subscription =>
                        Channel.GetStorageService(subscription, storageServiceInput.ServiceName));
                }
                while (storageService.StorageServiceProperties.Status != StorageAccountStatus.Created);
            });
        }

        /// <summary>
        /// Get the configuration.
        /// </summary>
        /// <returns>Configuration.</returns>
        private string GetConfiguration()
        {
            string configurationPath = _azureService.Paths.CloudConfiguration;
            Validate.ValidateFileFull(configurationPath, Resources.ServiceConfiguration);
            string fullPath = ResolvePath(configurationPath);
            string configuration = Utility.GetConfiguration(fullPath);
            return configuration;
        }

        /// <summary>
        /// Create a new deployment for the service.
        /// </summary>
        private void CreateNewDeployment()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_hostedServiceName),
                "_hostedServiceName cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.Slot),
                "Slot cannot be null or empty.");

            CreateDeploymentInput deploymentInput = new CreateDeploymentInput
            {
                PackageUrl = UploadPackage(),
                Configuration = GetConfiguration(),
                Label = ServiceManagementHelper.EncodeToBase64String(_deploymentSettings.Label),
                Name = _deploymentSettings.DeploymentName,
                StartDeployment = true,
            };

            CertificateList uploadedCertificates = RetryCall<CertificateList>(subscription => Channel.ListCertificates(subscription, _hostedServiceName));
            AddCertificates(uploadedCertificates);
            InvokeInOperationContext(() =>
                {

                    RetryCall(subscription =>
                        Channel.CreateOrUpdateDeployment(
                            subscription,
                            _hostedServiceName,
                            _deploymentSettings.ServiceSettings.Slot,
                            deploymentInput));
                    WaitForDeploymentToStart();
                });
        }

        /// <summary>
        /// Upgrade the deployment for the service.
        /// </summary>
        private void UpgradeDeployment()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_hostedServiceName),
                "_hostedServiceName cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.Label),
                "Label cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.DeploymentName),
                "DeploymentName cannot be null or empty.");

            UpgradeDeploymentInput deploymentInput = new UpgradeDeploymentInput
            {
                PackageUrl = UploadPackage(),
                Configuration = GetConfiguration(),
                Label = ServiceManagementHelper.EncodeToBase64String(_deploymentSettings.Label),
                Mode = UpgradeType.Auto
            };

            CertificateList uploadedCertificates = RetryCall<CertificateList>(subscription => Channel.ListCertificates(subscription, _hostedServiceName));
            AddCertificates(uploadedCertificates);
            InvokeInOperationContext(() =>
            {
                SafeWriteObjectWithTimestamp(Resources.PublishUpgradingMessage);
                RetryCall(subscription =>
                    Channel.UpgradeDeployment(
                        subscription,
                        _hostedServiceName,
                        _deploymentSettings.DeploymentName,
                        deploymentInput));
                WaitForDeploymentToStart();
            });
        }

        /// <summary>
        /// Wait until a certificate has been added to a hosted service.
        /// </summary>
        private void WaitForCertificateToBeAdded(ServiceConfigurationSchema.Certificate certificate)
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_hostedServiceName),
                "_hostedServiceName cannot be null or empty.");

            CertificateList certificates = null;
            do
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                certificates = RetryCall<CertificateList>(subscription =>
                    Channel.ListCertificates(subscription, _hostedServiceName));
            }
            while (certificates == null || certificates.Count<Certificate>(c => c.Thumbprint.Equals(
                certificate.thumbprint, StringComparison.OrdinalIgnoreCase)) < 1);
        }

        /// <summary>
        /// Wait until a deployment has started.
        /// </summary>
        private void WaitForDeploymentToStart()
        {
            Debug.Assert(
                !string.IsNullOrEmpty(_hostedServiceName),
                "_hostedServiceName cannot be null or empty.");
            Debug.Assert(
                !string.IsNullOrEmpty(_deploymentSettings.ServiceSettings.Slot),
                "Slot cannot be null or empty.");

            Deployment deployment = null;
            do
            {
                deployment = RetryCall(subscription =>
                    Channel.GetDeploymentBySlot(
                        subscription,
                        _hostedServiceName,
                        _deploymentSettings.ServiceSettings.Slot));
            }
            while (deployment.Status != DeploymentStatus.Starting &&
                deployment.Status != DeploymentStatus.Running);

            SafeWriteObjectWithTimestamp(string.Format(Resources.PublishCreatedDeploymentMessage,
               deployment.PrivateID));

        }

        /// <summary>
        /// Verify a deployment exists
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="slot"></param>
        private void VerifyDeployment()
        {
            try
            {
                SafeWriteObjectWithTimestamp(Resources.PublishStartingMessage);
                SafeWriteObjectWithTimestamp(Resources.PublishInitializingMessage);

                Dictionary<string, RoleInstance> roleInstanceSnapshot = new Dictionary<string, RoleInstance>();

                // Continue polling for deployment until all of the roles
                // indicate they're ready
                Deployment deployment = new Deployment();
                do
                {
                    deployment = RetryCall(subscription =>
                        Channel.GetDeploymentBySlot(
                            subscription,
                            _hostedServiceName,
                            _deploymentSettings.ServiceSettings.Slot));

                    // The goal of this loop is to output a message whenever the status of a role 
                    // instance CHANGES. To do that, we have to remember the last status of all role instances
                    // and that's what the roleInstanceSnapshot array is for
                    foreach (RoleInstance currentInstance in deployment.RoleInstanceList)
                    {
                        // We only care about these three statuses, ignore other intermediate statuses
                        if (String.Equals(currentInstance.InstanceStatus, RoleInstanceStatus.Busy) ||
                            String.Equals(currentInstance.InstanceStatus, RoleInstanceStatus.Ready) ||
                            String.Equals(currentInstance.InstanceStatus, RoleInstanceStatus.Initializing))
                        {
                            bool createdOrChanged = false;

                            // InstanceName is unique and concatenates the role name and instance name
                            if (roleInstanceSnapshot.ContainsKey(currentInstance.InstanceName))
                            {
                                // If we already have a snapshot of that role instance, update it
                                RoleInstance previousInstance = roleInstanceSnapshot[currentInstance.InstanceName];
                                if (!String.Equals(previousInstance.InstanceStatus, currentInstance.InstanceStatus))
                                {
                                    // If the instance status changed, we need to output a message
                                    previousInstance.InstanceStatus = currentInstance.InstanceStatus;
                                    createdOrChanged = true;
                                }
                            }
                            else
                            {
                                // If this is the first time we run through, we also need to output a message
                                roleInstanceSnapshot[currentInstance.InstanceName] = currentInstance;
                                createdOrChanged = true;
                            }


                            if (createdOrChanged)
                            {
                                string statusResource;
                                switch (currentInstance.InstanceStatus)
                                {
                                    case RoleInstanceStatus.Busy:
                                        statusResource = Resources.PublishInstanceStatusBusy;
                                        break;

                                    case RoleInstanceStatus.Ready:
                                        statusResource = Resources.PublishInstanceStatusReady;
                                        break;

                                    default:
                                        statusResource = Resources.PublishInstanceStatusCreating;
                                        break;
                                }

                                SafeWriteObjectWithTimestamp(String.Format(Resources.PublishInstanceStatusMessage,
                                    currentInstance.InstanceName, currentInstance.RoleName, statusResource));

                            }
                        }
                    }

                    // If a deployment has many roles to initialize, this
                    // thread must throttle requests so the Azure portal
                    // doesn't reply with a "too many requests" error
                    Thread.Sleep(int.Parse(Resources.StandardRetryDelayInMs));
                }
                while (deployment.RoleInstanceList.Any(
                    r => r.InstanceStatus != RoleInstanceStatus.Ready));

                if (CanGenerateUrlForDeploymentSlot())
                {
                    SafeWriteObjectWithTimestamp(
                        Resources.PublishCreatedWebsiteMessage,
                        string.Format(Resources.ServiceUrl, _hostedServiceName));
                }
                else
                {
                    SafeWriteObjectWithTimestamp(
                        Resources.PublishCreatedWebsiteLaunchNotSupportedMessage);
                }

            }
            catch (EndpointNotFoundException)
            {
                throw new InvalidOperationException(
                    string.Format(
                        Resources.CannotFindDeployment,
                        _hostedServiceName,
                        _deploymentSettings.ServiceSettings.Slot));
            }
        }

        private void AddCertificates(CertificateList uploadedCertificates)
        {
            if (_azureService.Components.CloudConfig.Role != null)
            {
                foreach (ServiceConfigurationSchema.Certificate certElement in _azureService.Components.CloudConfig.Role.
                    SelectMany(r => (null == r.Certificates) ? new ServiceConfigurationSchema.Certificate[0] : r.Certificates).Distinct())
                {
                    if (uploadedCertificates == null || (uploadedCertificates.Count<Certificate>(c => c.Thumbprint.Equals(
                        certElement.thumbprint, StringComparison.OrdinalIgnoreCase)) < 1))
                    {
                        X509Certificate2 cert = General.GetCertificateFromStore(certElement.thumbprint);
                        CertificateFile certFile = null;
                        try
                        {
                            certFile = new CertificateFile
                            {
                                Data = Convert.ToBase64String(cert.Export(X509ContentType.Pfx, string.Empty)),
                                Password = string.Empty,
                                CertificateFormat = "pfx"
                            };
                        }
                        catch (CryptographicException exception)
                        {
                            throw new ArgumentException(string.Format(Resources.CertificatePrivateKeyAccessError, certElement.name), exception);
                        }

                        RetryCall(subscription => Channel.AddCertificates(subscription, _hostedServiceName, certFile));
                        WaitForCertificateToBeAdded(certElement);
                    }
                }
            }
        }

        /// <summary>
        /// Launch a browser pointed at the service after deploying.
        /// </summary>
        [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
        private void LaunchService()
        {
            Debug.Assert(!string.IsNullOrEmpty(_hostedServiceName), "_hostedServiceName cannot be null or empty.");
            General.LaunchWebPage(string.Format(Resources.ServiceUrl, _hostedServiceName));
        }

        /// <summary>
        /// Create a channel to be used for managing Azure services.
        /// </summary>
        /// <returns>The created channel.</returns>
        protected override IServiceManagement CreateChannel()
        {
            // If ShareChannel is set by a unit test, use the same channel that
            // was passed into out constructor.  This allows the test to submit
            // a mock that we use for all network calls.
            return ShareChannel ?
                Channel :
                base.CreateChannel();
        }
    }
}
