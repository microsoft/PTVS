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
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Collections.ObjectModel;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet
{

    /// <summary>
    /// List of operating system families.
    /// </summary>
    [CollectionDataContract(Name = "OperatingSystemFamilies", ItemName = "OperatingSystemFamily", Namespace = Constants.ServiceManagementNS)]
    public class OperatingSystemFamilyList : List<OperatingSystemFamily>
    {
        public OperatingSystemFamilyList()
        {
        }

        public OperatingSystemFamilyList(IEnumerable<OperatingSystemFamily> operatingSystemFamilies)
            : base(operatingSystemFamilies)
        {
        }
    }

    /// <summary>
    /// An operating system family supported in Windows Azure.
    /// </summary>
    [DataContract(Namespace = Constants.ServiceManagementNS)]
    public class OperatingSystemFamily : IExtensibleDataObject
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public string Label { get; set; }

        [DataMember(Order = 3)]
        public OperatingSystemList OperatingSystems { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }

    /// <summary>
    /// List of operating systems.
    /// </summary>
    [CollectionDataContract(Name = "OperatingSystems", ItemName = "OperatingSystem", Namespace = Constants.ServiceManagementNS)]
    public class OperatingSystemList : List<OperatingSystem>
    {
        public OperatingSystemList()
        {
        }

        public OperatingSystemList(IEnumerable<OperatingSystem> operatingSystems)
            : base(operatingSystems)
        {
        }
    }

    /// <summary>
    /// An operating system supported in Windows Azure.
    /// </summary>
    [DataContract(Namespace = Constants.ServiceManagementNS)]
    public class OperatingSystem : IExtensibleDataObject
    {
        [DataMember(Order = 1)]
        public string Version { get; set; }

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public string Label { get; set; }

        [DataMember(Order = 3)]
        public bool IsDefault { get; set; }

        [DataMember(Order = 4)]
        public bool IsActive { get; set; }

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public string Family { get; set; }

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public string FamilyLabel { get; set; }

        public ExtensionDataObject ExtensionData { get; set; }
    }

    /// <summary>
    /// The operating-system-specific interface of the resource model service.
    /// </summary>
    public partial interface IServiceManagement
    {
        #region List Operating Systems

        /// <summary>
        /// Lists all available operating systems.
        /// </summary>
        [OperationContract(AsyncPattern = true)]
        [WebInvoke(Method = "GET", UriTemplate = @"{subscriptionId}/operatingsystems")]
        IAsyncResult BeginListOperatingSystems(string subscriptionId, AsyncCallback callback, object state);
        OperatingSystemList EndListOperatingSystems(IAsyncResult asyncResult);

        #endregion

        #region List Operating Systems Families

        /// <summary>
        /// Lists all available operating system families and their operating systems.
        /// </summary>
        [OperationContract(AsyncPattern = true)]
        [WebInvoke(Method = "GET", UriTemplate = @"{subscriptionId}/operatingsystemfamilies")]
        IAsyncResult BeginListOperatingSystemFamilies(string subscriptionId, AsyncCallback callback, object state);
        OperatingSystemFamilyList EndListOperatingSystemFamilies(IAsyncResult asyncResult);

        #endregion
    }

    /// <summary>
    /// Extensions of the IServiceManagement interface that allows clients to call operations synchronously.
    /// </summary>
    public static partial class ServiceManagementExtensionMethods
    {
        public static OperatingSystemList ListOperatingSystems(this IServiceManagement proxy, string subscriptionId)
        {
            return proxy.EndListOperatingSystems(proxy.BeginListOperatingSystems(subscriptionId, null, null));
        }

        public static OperatingSystemFamilyList ListOperatingSystemFamilies(this IServiceManagement proxy, string subscriptionId)
        {
            return proxy.EndListOperatingSystemFamilies(proxy.BeginListOperatingSystemFamilies(subscriptionId, null, null));
        }
    }
}
