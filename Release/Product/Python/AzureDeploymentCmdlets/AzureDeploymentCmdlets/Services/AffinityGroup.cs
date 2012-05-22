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
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{
    /// <summary>
    /// List of affinity groups.
    /// </summary>
    [CollectionDataContract(Name = "AffinityGroups", ItemName = "AffinityGroup", Namespace = Constants.ServiceManagementNS)]
    public class AffinityGroupList : List<AffinityGroup>
    {
        public AffinityGroupList()
        {
        }

        public AffinityGroupList(IEnumerable<AffinityGroup> affinityGroups)
            : base(affinityGroups)
        {
        }
    }

    /// <summary>
    /// Affinity Group data contract. 
    /// </summary>
    [DataContract(Namespace = Constants.ServiceManagementNS)]
    public class AffinityGroup : IExtensibleDataObject
    {
        [DataMember(Order = 1, EmitDefaultValue = false)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public string Label { get; set; }

        [DataMember(Order = 3)]
        public string Description { get; set; }

        [DataMember(Order = 4)]
        public string Location { get; set; }

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public HostedServiceList HostedServices { get; set; }

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet.StorageServiceList StorageServices { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }

    /// <summary>
    /// The affinity group related part of the external API
    /// </summary>
    public partial interface IServiceManagement
    {
        /// <summary>
        /// Lists the affinity groups associated with the specified subscription.
        /// </summary>
        [OperationContract(AsyncPattern = true)]
        [WebGet(UriTemplate = @"{subscriptionId}/affinitygroups")]
        IAsyncResult BeginListAffinityGroups(string subscriptionId, AsyncCallback callback, object state);
        AffinityGroupList EndListAffinityGroups(IAsyncResult asyncResult);

        /// <summary>
        /// Get properties for the specified affinity group. 
        /// </summary>
        [OperationContract(AsyncPattern = true)]
        [WebGet(UriTemplate = @"{subscriptionId}/affinitygroups/{affinityGroupName}")]
        IAsyncResult BeginGetAffinityGroup(string subscriptionId, string affinityGroupName, AsyncCallback callback, object state);
        AffinityGroup EndGetAffinityGroup(IAsyncResult asyncResult);
    }

    public static partial class ServiceManagementExtensionMethods
    {
        public static AffinityGroupList ListAffinityGroups(this IServiceManagement proxy, string subscriptionId)
        {
            return proxy.EndListAffinityGroups(proxy.BeginListAffinityGroups(subscriptionId, null, null));
        }

        public static AffinityGroup GetAffinityGroup(this IServiceManagement proxy, string subscriptionId, string affinityGroupName)
        {
            return proxy.EndGetAffinityGroup(proxy.BeginGetAffinityGroup(subscriptionId, affinityGroupName, null, null));
        }
    }
}
