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
using System.ServiceModel;
using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ServiceModel.Channels;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test
{
    /// <summary>
    /// Simple IAsyncResult implementation that can be used to cache all the
    /// parameters to the BeginFoo call and then passed to the FooThunk
    /// property that's invoked by EndFoo (thereby providing the test's
    /// implementation of FooThunk with as much of the state as it wants).
    /// </summary>
    public class SimpleServiceManagementAsyncResult : IAsyncResult
    {
        /// <summary>
        /// Gets a dictionary of state specific to a given call.
        /// </summary>
        public Dictionary<string, object> Values { get; private set; }
        
        /// <summary>
        /// Initializes a new instance of the
        /// SimpleServiceManagementAsyncResult class.
        /// </summary>
        public SimpleServiceManagementAsyncResult()
        {
            Values = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Gets the state specific to a given call.
        /// </summary>
        public object AsyncState
        {
            get { return Values; }
        }
        
        /// <summary>
        /// Gets an AsyncWaitHandle.  This is not implemented and will always
        /// throw.
        /// </summary>
        public WaitHandle AsyncWaitHandle
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Gets a value indicating whether the async call completed
        /// synchronousy.  It always returns true.
        /// </summary>
        public bool CompletedSynchronously
        {
            get { return true; }
        }
        
        /// <summary>
        /// Gets a value indicating whether the async call has completed.  It
        /// always returns true.
        /// </summary>
        public bool IsCompleted
        {
            get { return true; }
        }
    }

    /// <summary>
    /// Simple implementation of teh IServiceManagement interface that can be
    /// used for mocking basic interactions without involving Azure directly.
    /// </summary>
    public class SimpleServiceManagement : IServiceManagement
    {
        /// <summary>
        /// Gets or sets a value indicating whether the thunk wrappers will
        /// throw an exception if the thunk is not implemented.  This is useful
        /// when debugging a test.
        /// </summary>
        public bool ThrowsIfNotImplemented { get; set; }

        /// <summary>
        /// Initializes a new instance of the SimpleServiceManagement class.
        /// </summary>
        public SimpleServiceManagement()
        {
            ThrowsIfNotImplemented = true;
        }

        public Action<SimpleServiceManagementAsyncResult> AddCertificatesThunk { get; set; }
        public IAsyncResult BeginAddCertificates(string subscriptionId, string serviceName, CertificateFile input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }
        public void EndAddCertificates(IAsyncResult asyncResult)
        {
            if (AddCertificatesThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                AddCertificatesThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("AddCertificatesThunk is not implemented!");
            }
        }

        public Func<SimpleServiceManagementAsyncResult, CertificateList> ListCertificatesThunk { get; set; }
        public IAsyncResult BeginListCertificates(string subscriptionId, string serviceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public CertificateList EndListCertificates(IAsyncResult asyncResult)
        {
            CertificateList certificates = default(CertificateList);
            if (ListCertificatesThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                return ListCertificatesThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListCertificatesThunk is not implemented!");
            }

            return certificates;
        }

        #region Autogenerated Thunks

        #region ChangeConfiguration
        public Action<SimpleServiceManagementAsyncResult> ChangeConfigurationThunk { get; set; }

        public IAsyncResult BeginChangeConfiguration(string subscriptionId, string serviceName, string deploymentName, ChangeConfigurationInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndChangeConfiguration(IAsyncResult asyncResult)
        {
            if (ChangeConfigurationThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                ChangeConfigurationThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ChangeConfigurationThunk is not implemented!");
            }
        }
        #endregion ChangeConfiguration

        #region ChangeConfigurationBySlot
        public Action<SimpleServiceManagementAsyncResult> ChangeConfigurationBySlotThunk { get; set; }

        public IAsyncResult BeginChangeConfigurationBySlot(string subscriptionId, string serviceName, string deploymentSlot, ChangeConfigurationInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndChangeConfigurationBySlot(IAsyncResult asyncResult)
        {
            if (ChangeConfigurationBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                ChangeConfigurationBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ChangeConfigurationBySlotThunk is not implemented!");
            }
        }
        #endregion ChangeConfigurationBySlot

        #region UpdateDeploymentStatus
        public Action<SimpleServiceManagementAsyncResult> UpdateDeploymentStatusThunk { get; set; }

        public IAsyncResult BeginUpdateDeploymentStatus(string subscriptionId, string serviceName, string deploymentName, UpdateDeploymentStatusInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndUpdateDeploymentStatus(IAsyncResult asyncResult)
        {
            if (UpdateDeploymentStatusThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                UpdateDeploymentStatusThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("UpdateDeploymentStatusThunk is not implemented!");
            }
        }
        #endregion UpdateDeploymentStatus

        #region UpdateDeploymentStatusBySlot
        public Action<SimpleServiceManagementAsyncResult> UpdateDeploymentStatusBySlotThunk { get; set; }

        public IAsyncResult BeginUpdateDeploymentStatusBySlot(string subscriptionId, string serviceName, string deploymentSlot, UpdateDeploymentStatusInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndUpdateDeploymentStatusBySlot(IAsyncResult asyncResult)
        {
            if (UpdateDeploymentStatusBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                UpdateDeploymentStatusBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("UpdateDeploymentStatusBySlotThunk is not implemented!");
            }
        }
        #endregion UpdateDeploymentStatusBySlot

        #region UpgradeDeployment
        public Action<SimpleServiceManagementAsyncResult> UpgradeDeploymentThunk { get; set; }

        public IAsyncResult BeginUpgradeDeployment(string subscriptionId, string serviceName, string deploymentName, UpgradeDeploymentInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndUpgradeDeployment(IAsyncResult asyncResult)
        {
            if (UpgradeDeploymentThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                UpgradeDeploymentThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("UpgradeDeploymentThunk is not implemented!");
            }
        }
        #endregion UpgradeDeployment

        #region UpgradeDeploymentBySlot
        public Action<SimpleServiceManagementAsyncResult> UpgradeDeploymentBySlotThunk { get; set; }

        public IAsyncResult BeginUpgradeDeploymentBySlot(string subscriptionId, string serviceName, string deploymentSlot, UpgradeDeploymentInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndUpgradeDeploymentBySlot(IAsyncResult asyncResult)
        {
            if (UpgradeDeploymentBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                UpgradeDeploymentBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("UpgradeDeploymentBySlotThunk is not implemented!");
            }
        }
        #endregion UpgradeDeploymentBySlot

        #region WalkUpgradeDomain
        public Action<SimpleServiceManagementAsyncResult> WalkUpgradeDomainThunk { get; set; }

        public IAsyncResult BeginWalkUpgradeDomain(string subscriptionId, string serviceName, string deploymentName, WalkUpgradeDomainInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndWalkUpgradeDomain(IAsyncResult asyncResult)
        {
            if (WalkUpgradeDomainThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                WalkUpgradeDomainThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("WalkUpgradeDomainThunk is not implemented!");
            }
        }
        #endregion WalkUpgradeDomain

        #region WalkUpgradeDomainBySlot
        public Action<SimpleServiceManagementAsyncResult> WalkUpgradeDomainBySlotThunk { get; set; }

        public IAsyncResult BeginWalkUpgradeDomainBySlot(string subscriptionId, string serviceName, string deploymentSlot, WalkUpgradeDomainInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndWalkUpgradeDomainBySlot(IAsyncResult asyncResult)
        {
            if (WalkUpgradeDomainBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                WalkUpgradeDomainBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("WalkUpgradeDomainBySlotThunk is not implemented!");
            }
        }
        #endregion WalkUpgradeDomainBySlot

        #region RebootDeploymentRoleInstance
        public Action<SimpleServiceManagementAsyncResult> RebootDeploymentRoleInstanceThunk { get; set; }

        public IAsyncResult BeginRebootDeploymentRoleInstance(string subscriptionId, string serviceName, string deploymentName, string roleInstanceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["roleInstanceName"] = roleInstanceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndRebootDeploymentRoleInstance(IAsyncResult asyncResult)
        {
            if (RebootDeploymentRoleInstanceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                RebootDeploymentRoleInstanceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("RebootDeploymentRoleInstanceThunk is not implemented!");
            }
        }
        #endregion RebootDeploymentRoleInstance

        #region ReimageDeploymentRoleInstance
        public Action<SimpleServiceManagementAsyncResult> ReimageDeploymentRoleInstanceThunk { get; set; }

        public IAsyncResult BeginReimageDeploymentRoleInstance(string subscriptionId, string serviceName, string deploymentName, string roleInstanceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["roleInstanceName"] = roleInstanceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndReimageDeploymentRoleInstance(IAsyncResult asyncResult)
        {
            if (ReimageDeploymentRoleInstanceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                ReimageDeploymentRoleInstanceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ReimageDeploymentRoleInstanceThunk is not implemented!");
            }
        }
        #endregion ReimageDeploymentRoleInstance

        #region RebootDeploymentRoleInstanceBySlot
        public Action<SimpleServiceManagementAsyncResult> RebootDeploymentRoleInstanceBySlotThunk { get; set; }

        public IAsyncResult BeginRebootDeploymentRoleInstanceBySlot(string subscriptionId, string serviceName, string deploymentSlot, string roleInstanceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["roleInstanceName"] = roleInstanceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndRebootDeploymentRoleInstanceBySlot(IAsyncResult asyncResult)
        {
            if (RebootDeploymentRoleInstanceBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                RebootDeploymentRoleInstanceBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("RebootDeploymentRoleInstanceBySlotThunk is not implemented!");
            }
        }
        #endregion RebootDeploymentRoleInstanceBySlot

        #region ReimageDeploymentRoleInstanceBySlot
        public Action<SimpleServiceManagementAsyncResult> ReimageDeploymentRoleInstanceBySlotThunk { get; set; }

        public IAsyncResult BeginReimageDeploymentRoleInstanceBySlot(string subscriptionId, string serviceName, string deploymentSlot, string roleInstanceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["roleInstanceName"] = roleInstanceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndReimageDeploymentRoleInstanceBySlot(IAsyncResult asyncResult)
        {
            if (ReimageDeploymentRoleInstanceBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                ReimageDeploymentRoleInstanceBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ReimageDeploymentRoleInstanceBySlotThunk is not implemented!");
            }
        }
        #endregion ReimageDeploymentRoleInstanceBySlot

        #region UpdateHostedService
        public Action<SimpleServiceManagementAsyncResult> UpdateHostedServiceThunk { get; set; }

        public IAsyncResult BeginUpdateHostedService(string subscriptionId, string serviceName, UpdateHostedServiceInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndUpdateHostedService(IAsyncResult asyncResult)
        {
            if (UpdateHostedServiceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                UpdateHostedServiceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("UpdateHostedServiceThunk is not implemented!");
            }
        }
        #endregion UpdateHostedService

        #region DeleteHostedService
        public Action<SimpleServiceManagementAsyncResult> DeleteHostedServiceThunk { get; set; }

        public IAsyncResult BeginDeleteHostedService(string subscriptionId, string serviceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndDeleteHostedService(IAsyncResult asyncResult)
        {
            if (DeleteHostedServiceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                DeleteHostedServiceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("DeleteHostedServiceThunk is not implemented!");
            }
        }
        #endregion DeleteHostedService

        #region ListHostedServices
        public Func<SimpleServiceManagementAsyncResult, HostedServiceList> ListHostedServicesThunk { get; set; }

        public IAsyncResult BeginListHostedServices(string subscriptionId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public HostedServiceList EndListHostedServices(IAsyncResult asyncResult)
        {
            HostedServiceList value = default(HostedServiceList);
            if (ListHostedServicesThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = ListHostedServicesThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListHostedServicesThunk is not implemented!");
            }

            return value;
        }
        #endregion ListHostedServices

        #region GetHostedService
        public Func<SimpleServiceManagementAsyncResult, HostedService> GetHostedServiceThunk { get; set; }

        public IAsyncResult BeginGetHostedService(string subscriptionId, string serviceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public HostedService EndGetHostedService(IAsyncResult asyncResult)
        {
            HostedService value = default(HostedService);
            if (GetHostedServiceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetHostedServiceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetHostedServiceThunk is not implemented!");
            }

            return value;
        }
        #endregion GetHostedService

        #region GetHostedServiceWithDetails
        public Func<SimpleServiceManagementAsyncResult, HostedService> GetHostedServiceWithDetailsThunk { get; set; }

        public IAsyncResult BeginGetHostedServiceWithDetails(string subscriptionId, string serviceName, Boolean embedDetail, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["embedDetail"] = embedDetail;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public HostedService EndGetHostedServiceWithDetails(IAsyncResult asyncResult)
        {
            HostedService value = default(HostedService);
            if (GetHostedServiceWithDetailsThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetHostedServiceWithDetailsThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetHostedServiceWithDetailsThunk is not implemented!");
            }

            return value;
        }
        #endregion GetHostedServiceWithDetails

        #region ListLocations
        public Func<SimpleServiceManagementAsyncResult, LocationList> ListLocationsThunk { get; set; }

        public IAsyncResult BeginListLocations(string subscriptionId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public LocationList EndListLocations(IAsyncResult asyncResult)
        {
            LocationList value = default(LocationList);
            if (ListLocationsThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = ListLocationsThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListLocationsThunk is not implemented!");
            }

            return value;
        }
        #endregion ListLocations

        #region SwapDeployment
        public Action<SimpleServiceManagementAsyncResult> SwapDeploymentThunk { get; set; }

        public IAsyncResult BeginSwapDeployment(string subscriptionId, string serviceName, SwapDeploymentInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndSwapDeployment(IAsyncResult asyncResult)
        {
            if (SwapDeploymentThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                SwapDeploymentThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("SwapDeploymentThunk is not implemented!");
            }
        }
        #endregion SwapDeployment

        #region CreateOrUpdateDeployment
        public Action<SimpleServiceManagementAsyncResult> CreateOrUpdateDeploymentThunk { get; set; }

        public IAsyncResult BeginCreateOrUpdateDeployment(string subscriptionId, string serviceName, string deploymentSlot, CreateDeploymentInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndCreateOrUpdateDeployment(IAsyncResult asyncResult)
        {
            if (CreateOrUpdateDeploymentThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                CreateOrUpdateDeploymentThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("CreateOrUpdateDeploymentThunk is not implemented!");
            }
        }
        #endregion CreateOrUpdateDeployment

        #region DeleteDeployment
        public Action<SimpleServiceManagementAsyncResult> DeleteDeploymentThunk { get; set; }

        public IAsyncResult BeginDeleteDeployment(string subscriptionId, string serviceName, string deploymentName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndDeleteDeployment(IAsyncResult asyncResult)
        {
            if (DeleteDeploymentThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                DeleteDeploymentThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("DeleteDeploymentThunk is not implemented!");
            }
        }
        #endregion DeleteDeployment

        #region DeleteDeploymentBySlot
        public Action<SimpleServiceManagementAsyncResult> DeleteDeploymentBySlotThunk { get; set; }

        public IAsyncResult BeginDeleteDeploymentBySlot(string subscriptionId, string serviceName, string deploymentSlot, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndDeleteDeploymentBySlot(IAsyncResult asyncResult)
        {
            if (DeleteDeploymentBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                DeleteDeploymentBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("DeleteDeploymentBySlotThunk is not implemented!");
            }
        }
        #endregion DeleteDeploymentBySlot

        #region GetDeployment
        public Func<SimpleServiceManagementAsyncResult, Deployment> GetDeploymentThunk { get; set; }

        public IAsyncResult BeginGetDeployment(string subscriptionId, string serviceName, string deploymentName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentName"] = deploymentName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public Deployment EndGetDeployment(IAsyncResult asyncResult)
        {
            Deployment value = default(Deployment);
            if (GetDeploymentThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetDeploymentThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetDeploymentThunk is not implemented!");
            }

            return value;
        }
        #endregion GetDeployment

        #region GetDeploymentBySlot
        public Func<SimpleServiceManagementAsyncResult, Deployment> GetDeploymentBySlotThunk { get; set; }

        public IAsyncResult BeginGetDeploymentBySlot(string subscriptionId, string serviceName, string deploymentSlot, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["deploymentSlot"] = deploymentSlot;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public Deployment EndGetDeploymentBySlot(IAsyncResult asyncResult)
        {
            Deployment value = default(Deployment);
            if (GetDeploymentBySlotThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetDeploymentBySlotThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetDeploymentBySlotThunk is not implemented!");
            }

            return value;
        }
        #endregion GetDeploymentBySlot

        #region ListOperatingSystems
        public Func<SimpleServiceManagementAsyncResult, OperatingSystemList> ListOperatingSystemsThunk { get; set; }

        public IAsyncResult BeginListOperatingSystems(string subscriptionId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public OperatingSystemList EndListOperatingSystems(IAsyncResult asyncResult)
        {
            OperatingSystemList value = default(OperatingSystemList);
            if (ListOperatingSystemsThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = ListOperatingSystemsThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListOperatingSystemsThunk is not implemented!");
            }

            return value;
        }
        #endregion ListOperatingSystems

        #region ListOperatingSystemFamilies
        public Func<SimpleServiceManagementAsyncResult, OperatingSystemFamilyList> ListOperatingSystemFamiliesThunk { get; set; }

        public IAsyncResult BeginListOperatingSystemFamilies(string subscriptionId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public OperatingSystemFamilyList EndListOperatingSystemFamilies(IAsyncResult asyncResult)
        {
            OperatingSystemFamilyList value = default(OperatingSystemFamilyList);
            if (ListOperatingSystemFamiliesThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = ListOperatingSystemFamiliesThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListOperatingSystemFamiliesThunk is not implemented!");
            }

            return value;
        }
        #endregion ListOperatingSystemFamilies

        #region GetOperationStatus
        public Func<SimpleServiceManagementAsyncResult, Operation> GetOperationStatusThunk { get; set; }

        public IAsyncResult BeginGetOperationStatus(string subscriptionId, string operationTrackingId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["operationTrackingId"] = operationTrackingId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public Operation EndGetOperationStatus(IAsyncResult asyncResult)
        {
            Operation value = default(Operation);
            if (GetOperationStatusThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetOperationStatusThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetOperationStatusThunk is not implemented!");
            }

            return value;
        }
        #endregion GetOperationStatus

        #region ListStorageServices
        public Func<SimpleServiceManagementAsyncResult, StorageServiceList> ListStorageServicesThunk { get; set; }

        public IAsyncResult BeginListStorageServices(string subscriptionId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public StorageServiceList EndListStorageServices(IAsyncResult asyncResult)
        {
            StorageServiceList value = default(StorageServiceList);
            if (ListStorageServicesThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = ListStorageServicesThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListStorageServicesThunk is not implemented!");
            }

            return value;
        }
        #endregion ListStorageServices

        #region GetStorageService
        public Func<SimpleServiceManagementAsyncResult, StorageService> GetStorageServiceThunk { get; set; }

        public IAsyncResult BeginGetStorageService(string subscriptionId, string serviceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public StorageService EndGetStorageService(IAsyncResult asyncResult)
        {
            StorageService value = default(StorageService);
            if (GetStorageServiceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetStorageServiceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetStorageServiceThunk is not implemented!");
            }

            return value;
        }
        #endregion GetStorageService

        #region GetStorageKeys
        public Func<SimpleServiceManagementAsyncResult, StorageService> GetStorageKeysThunk { get; set; }

        public IAsyncResult BeginGetStorageKeys(string subscriptionId, string serviceName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public StorageService EndGetStorageKeys(IAsyncResult asyncResult)
        {
            StorageService value = default(StorageService);
            if (GetStorageKeysThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetStorageKeysThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetStorageKeysThunk is not implemented!");
            }

            return value;
        }
        #endregion GetStorageKeys

        #region RegenerateStorageServiceKeys
        public Func<SimpleServiceManagementAsyncResult, StorageService> RegenerateStorageServiceKeysThunk { get; set; }

        public IAsyncResult BeginRegenerateStorageServiceKeys(string subscriptionId, string serviceName, RegenerateKeys regenerateKeys, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["serviceName"] = serviceName;
            result.Values["regenerateKeys"] = regenerateKeys;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public StorageService EndRegenerateStorageServiceKeys(IAsyncResult asyncResult)
        {
            StorageService value = default(StorageService);
            if (RegenerateStorageServiceKeysThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = RegenerateStorageServiceKeysThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("RegenerateStorageServiceKeysThunk is not implemented!");
            }

            return value;
        }
        #endregion RegenerateStorageServiceKeys

        #region CreateStorageAccount
        public Action<SimpleServiceManagementAsyncResult> CreateStorageAccountThunk { get; set; }

        public IAsyncResult BeginCreateStorageAccount(string subscriptionId, CreateStorageServiceInput createStorageServiceInput, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["createStorageServiceInput"] = createStorageServiceInput;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndCreateStorageAccount(IAsyncResult asyncResult)
        {
            if (CreateStorageAccountThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                CreateStorageAccountThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("CreateStorageAccountThunk is not implemented!");
            }
        }
        #endregion CreateStorageAccount

        #region DeleteStorageAccount
        public Action<SimpleServiceManagementAsyncResult> DeleteStorageAccountThunk { get; set; }

        public IAsyncResult BeginDeleteStorageAccount(string subscriptionId, string storageAccountName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["storageAccountName"] = storageAccountName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndDeleteStorageAccount(IAsyncResult asyncResult)
        {
            if (DeleteStorageAccountThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                DeleteStorageAccountThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("DeleteStorageAccountThunk is not implemented!");
            }
        }
        #endregion DeleteStorageAccount

        #region UpdateStorageAccount
        public Action<SimpleServiceManagementAsyncResult> UpdateStorageAccountThunk { get; set; }

        public IAsyncResult BeginUpdateStorageAccount(string subscriptionId, string storageAccountName, UpdateStorageServiceInput updateStorageServiceInput, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["storageAccountName"] = storageAccountName;
            result.Values["updateStorageServiceInput"] = updateStorageServiceInput;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndUpdateStorageAccount(IAsyncResult asyncResult)
        {
            if (UpdateStorageAccountThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                UpdateStorageAccountThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("UpdateStorageAccountThunk is not implemented!");
            }
        }
        #endregion UpdateStorageAccount

        #region ListAffinityGroups
        public Func<SimpleServiceManagementAsyncResult, AffinityGroupList> ListAffinityGroupsThunk { get; set; }

        public IAsyncResult BeginListAffinityGroups(string subscriptionId, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public AffinityGroupList EndListAffinityGroups(IAsyncResult asyncResult)
        {
            AffinityGroupList value = default(AffinityGroupList);
            if (ListAffinityGroupsThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = ListAffinityGroupsThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("ListAffinityGroupsThunk is not implemented!");
            }

            return value;
        }
        #endregion ListAffinityGroups

        #region GetAffinityGroup
        public Func<SimpleServiceManagementAsyncResult, AffinityGroup> GetAffinityGroupThunk { get; set; }

        public IAsyncResult BeginGetAffinityGroup(string subscriptionId, string affinityGroupName, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["affinityGroupName"] = affinityGroupName;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public AffinityGroup EndGetAffinityGroup(IAsyncResult asyncResult)
        {
            AffinityGroup value = default(AffinityGroup);
            if (GetAffinityGroupThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                value = GetAffinityGroupThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("GetAffinityGroupThunk is not implemented!");
            }

            return value;
        }
        #endregion GetAffinityGroup

        #region CreateHostedService
        public Action<SimpleServiceManagementAsyncResult> CreateHostedServiceThunk { get; set; }

        public IAsyncResult BeginCreateHostedService(string subscriptionId, CreateHostedServiceInput input, AsyncCallback callback, object state)
        {
            SimpleServiceManagementAsyncResult result = new SimpleServiceManagementAsyncResult();
            result.Values["subscriptionId"] = subscriptionId;
            result.Values["input"] = input;
            result.Values["callback"] = callback;
            result.Values["state"] = state;
            return result;
        }

        public void EndCreateHostedService(IAsyncResult asyncResult)
        {
            if (CreateHostedServiceThunk != null)
            {
                SimpleServiceManagementAsyncResult result = asyncResult as SimpleServiceManagementAsyncResult;
                Assert.IsNotNull(result, "asyncResult was not SimpleServiceManagementAsyncResult!");

                CreateHostedServiceThunk(result);
            }
            else if (ThrowsIfNotImplemented)
            {
                throw new NotImplementedException("CreateHostedServiceThunk is not implemented!");
            }
        }
        #endregion CreateHostedService

        #endregion Autogenerated Thunks
    }
}

