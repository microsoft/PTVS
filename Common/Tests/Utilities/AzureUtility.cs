// Visual Studio Shared Project
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
using System.Management.Automation;
using System.Threading;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace TestUtilities {
    public static class AzureUtility {
        public static class ToolsVersion {
            public static Version V22 = new Version(2, 2);
        }

        public static bool AzureToolsInstalled(Version azureToolsVersion) {
            var keyPath = string.Format(@"SOFTWARE\Classes\Installer\Dependencies\AzureTools_{0}.{1}_VS{2}0_Key",
                azureToolsVersion.Major,
                azureToolsVersion.Minor,
                AssemblyVersionInfo.VSMajorVersion);

            using (var key = Registry.LocalMachine.OpenSubKey(keyPath)) {
                return key != null;
            }
        }

        public static bool DeleteCloudServiceWithRetry(string subscriptionPublishSettingsFilePath, string serviceName) {
            for (int i = 0; i < 60; i++) {
                if (DeleteCloudService(subscriptionPublishSettingsFilePath, serviceName)) {
                    return true;
                }
                Thread.Sleep(2000);
            }

            return false;
        }

        public static bool DeleteWebSiteWithRetry(string subscriptionPublishSettingsFilePath, string webSiteName) {
            for (int i = 0; i < 5; i++) {
                if (DeleteWebSite(subscriptionPublishSettingsFilePath, webSiteName)) {
                    return true;
                }
                Thread.Sleep(2000);
            }

            return false;
        }

        public static bool DeleteCloudService(string subscriptionPublishSettingsFilePath, string serviceName) {
            var subscriptionName = FirstSubscriptionNameFromPublishSettings(subscriptionPublishSettingsFilePath);
            using (var ps = PowerShell.Create()) {
                ps.AddCommand("Set-ExecutionPolicy").AddParameter("Scope", "Process").AddParameter("ExecutionPolicy", "Unrestricted");
                ps.Invoke();
                ps.AddScript(@"
                        param($serviceName, $publishSettingsFile, $subscriptionName)
                        Import-AzurePublishSettingsFile -PublishSettingsFile $publishSettingsFile
                        Set-AzureSubscription -SubscriptionName $subscriptionName
                        Remove-AzureService -Force -DeleteAll -ServiceName $serviceName
                ");
                ps.AddParameter("publishSettingsFile", subscriptionPublishSettingsFilePath);
                ps.AddParameter("subscriptionName", subscriptionName);
                ps.AddParameter("serviceName", serviceName);
                ps.Invoke();
                return !ps.HadErrors;
            }
        }

        public static bool DeleteWebSite(string subscriptionPublishSettingsFilePath, string webSiteName) {
            var subscriptionName = FirstSubscriptionNameFromPublishSettings(subscriptionPublishSettingsFilePath);
            using (var ps = PowerShell.Create()) {
                ps.AddCommand("Set-ExecutionPolicy").AddParameter("Scope", "Process").AddParameter("ExecutionPolicy", "Unrestricted");
                ps.Invoke();
                ps.AddScript(@"
                        param($siteName, $publishSettingsFile, $subscriptionName)
                        Import-AzurePublishSettingsFile -PublishSettingsFile $publishSettingsFile
                        Set-AzureSubscription -SubscriptionName $subscriptionName
                        Remove-AzureWebsite -Force -Name $siteName
                ");
                ps.AddParameter("publishSettingsFile", subscriptionPublishSettingsFilePath);
                ps.AddParameter("subscriptionName", subscriptionName);
                ps.AddParameter("siteName", webSiteName);
                ps.Invoke();
                return !ps.HadErrors;
            }
        }

        private static string FirstSubscriptionNameFromPublishSettings(string publishSettingsFilePath) {
            XmlDocument doc = new XmlDocument();
            doc.Load(publishSettingsFilePath);
            var node = doc.SelectSingleNode("/PublishData/PublishProfile/Subscription/@Name");
            Assert.IsNotNull(node, "Could not find subscription in '{0}'", publishSettingsFilePath);
            return node.Value;
        }
    }
}
