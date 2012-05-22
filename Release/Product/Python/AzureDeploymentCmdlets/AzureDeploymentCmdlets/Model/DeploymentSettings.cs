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
using System.IO;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    public class DeploymentSettings
    {
        public ServiceSettings ServiceSettings { get; private set; }
        public string PackagePath { get; private set; }
        public string ConfigPath { get; private set; }
        public string Label { get; private set; }
        public string DeploymentName { get; private set; }
        public string SubscriptionId { get; private set; }

        public DeploymentSettings(ServiceSettings settings, string packagePath, string configPath, string label, string deploymentName)
        {
            Validate.ValidateNullArgument(settings, Resources.InvalidServiceSettingMessage);
            Validate.ValidateFileFull(packagePath, Resources.Package);
            Validate.ValidateFileFull(configPath, Resources.ServiceConfiguration);
            Validate.ValidateStringIsNullOrEmpty(label, "Label");
            Validate.ValidateStringIsNullOrEmpty(deploymentName, "Deployment name");

            this.ServiceSettings = settings;
            this.PackagePath = packagePath;
            this.ConfigPath = configPath;
            this.Label = label;
            this.DeploymentName = deploymentName;

            if (!string.IsNullOrEmpty(settings.Subscription))
            {
                GlobalComponents globalComponents = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory);
                SubscriptionId = globalComponents.GetSubscriptionId(settings.Subscription);
            }
            else
            {
                throw new ArgumentNullException("settings.Subscription", Resources.InvalidSubscriptionNameMessage);
            }
        }
    }
}