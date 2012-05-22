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
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;

    public class DeploymentInfoContext : ManagementOperationContext
    {
        private readonly XNamespace ns = "http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration";

        private Deployment innerDeployment = new Deployment();

        public DeploymentInfoContext(Deployment innerDeployment)
        {
            this.innerDeployment = innerDeployment;

            if (this.innerDeployment.RoleInstanceList != null)
            {
                this.RoleInstanceList = new List<Microsoft.PythonTools.AzureDeploymentCmdlets.Concrete.RoleInstance>();
                foreach (var roleInstance in this.innerDeployment.RoleInstanceList)
                {
                    this.RoleInstanceList.Add(new Microsoft.PythonTools.AzureDeploymentCmdlets.Concrete.RoleInstance(roleInstance));
                }
            }

            if (!string.IsNullOrEmpty(this.innerDeployment.Configuration))
            {
                string xmlString = ServiceManagementHelper.DecodeFromBase64String(this.innerDeployment.Configuration);

                XDocument doc = null;
                using (var stringReader = new StringReader(xmlString))
                {
                    XmlReader reader = XmlReader.Create(stringReader);
                    doc = XDocument.Load(reader);
                }

                this.OSVersion = doc.Root.Attribute("osVersion") != null ?
                                 doc.Root.Attribute("osVersion").Value :
                                 string.Empty;

                this.RolesConfiguration = new Dictionary<string, RoleConfiguration>();

                var roles = doc.Root.Descendants(this.ns + "Role");

                foreach (var role in roles)
                {
                    this.RolesConfiguration.Add(role.Attribute("name").Value, new RoleConfiguration(role));
                }
            }
        }

        public string Slot
        {
            get
            {
                return this.innerDeployment.DeploymentSlot;
            }
        }

        public string Name
        {
            get
            {
                return this.innerDeployment.Name;
            }
        }

        public Uri Url
        {
            get
            {
                return this.innerDeployment.Url;
            }
        }

        public string Status
        {
            get
            {
                return this.innerDeployment.Status;
            }
        }

        public IList<Microsoft.PythonTools.AzureDeploymentCmdlets.Concrete.RoleInstance> RoleInstanceList
        {
            get;
            protected set;
        }

        public string Configuration
        {
            get
            {
                return string.IsNullOrEmpty(this.innerDeployment.Configuration) ?
                    string.Empty :
                    ServiceManagementHelper.DecodeFromBase64String(this.innerDeployment.Configuration);
            }
        }

        public string DeploymentId
        {
            get
            {
                return this.innerDeployment.PrivateID;
            }
        }

        public string Label
        {
            get
            {
                return string.IsNullOrEmpty(this.innerDeployment.Label) ?
                    string.Empty :
                    ServiceManagementHelper.DecodeFromBase64String(this.innerDeployment.Label);
            }
        }

        public string OSVersion { get; set; }

        public IDictionary<string, RoleConfiguration> RolesConfiguration
        {
            get;
            protected set;
        }

        public XDocument SerializeRolesConfiguration()
        {
            XDocument document = new XDocument();

            XElement rootElement = new XElement(this.ns + "ServiceConfiguration");
            document.Add(rootElement);

            rootElement.SetAttributeValue("serviceName", this.ServiceName);
            rootElement.SetAttributeValue("osVersion", this.OSVersion);
            rootElement.SetAttributeValue("xmlns", this.ns.ToString());

            foreach (var roleConfig in this.RolesConfiguration)
            {
                rootElement.Add(roleConfig.Value.Serialize());
            }

            return document;
        }
    }
}
