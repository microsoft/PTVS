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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Model;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.WAPPSCmdlet;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Test.TestData
{
    static class Data
    {
        // To Do:
        // Add invalid service/storage account name data: http://social.msdn.microsoft.com/Forums/en-US/windowsazuredevelopment/thread/75b05a42-cd3b-4ab8-aa26-dc8366ede115
        // Add invalid deployment name data
        public static List<string> ValidServiceName { get; private set; }
        public static List<string> ValidSubscriptionName { get; private set; }
        public static List<string> ValidServiceRootName { get; private set; }
        public static List<string> ValidDeploymentName { get; private set; }
        public static List<string> ValidStorageName { get; private set; }
        public static List<string> ValidPublishSettings { get; private set; }
        public static List<string> ValidRoleName { get; private set; }
        public static List<int> ValidRoleInstances { get; private set; }
        public static List<string> InvalidServiceRootName { get; private set; }
        public static List<string> InvalidLocation { get; private set; }
        public static List<string> InvalidSlot { get; private set; }
        public static List<string> InvalidPublishSettings { get; private set; }
        public static List<string> InvalidServiceName { get; private set; }
        public static List<string> InvalidRoleName { get; private set; }
        public static List<string> InvalidFileName { get; private set; }
        public static List<string> InvalidPath { get; private set; }
        public static List<int> InvalidRoleInstances { get; private set; }
        public static StorageServiceList ValidStorageService { get; private set; }
        public static string AzureSdkAppDir { get; private set; }
        public static string TestResultDirectory { get; private set; }

        static Data()
        {
            AzureSdkAppDir = Path.Combine(Directory.GetCurrentDirectory(), Resources.AzureSdkDirectoryName);
            TestResultDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            ValidServiceName = new List<string>();
            InitializeValidServiceNameData();

            ValidSubscriptionName = new List<string>();
            InitializeValidSubscriptionNameData();

            ValidServiceRootName = new List<string>();
            InitializeValidServiceRootNameData();

            ValidDeploymentName = new List<string>();
            InitializeValidDeploymentNameData();

            ValidStorageName = new List<string>();
            InitializeValidStorageNameData();

            InvalidServiceRootName = new List<string>();
            InitializeInvalidServiceRootNameData();

            ValidPublishSettings = new List<string>();
            InitializeValidPublishSettingsData();

            InvalidPublishSettings = new List<string>();
            InitializeInvalidPublishSettingsData();

            InvalidLocation = new List<string>();
            InitializeInvalidLocationData();

            InvalidSlot = new List<string>();
            InitializeInvalidSlotData();

            InvalidServiceName = new List<string>();
            InitializeInvalidServiceNameData();

            ValidRoleName = new List<string>();
            InitializeValidRoleNameData();

            InvalidRoleName = new List<string>();
            InitializeInvalidRoleNameData();

            ValidRoleInstances = new List<int>();
            InitializeValidRoleInstancesData();

            InvalidRoleInstances = new List<int>();
            InitializeInvalidRoleInstancesData();

            InvalidFileName = new List<string>();
            InitializeInvalidFileNameData();

            InvalidPath = new List<string>();
            InitializeInvalidPathData();

            ValidStorageService = new StorageServiceList();
            InitializeValidStorageServiceData();
        }

        private static void InitializeValidStorageServiceData()
        {
            StorageService myStore = new StorageService();
            myStore.ServiceName = "mystore";
            myStore.StorageServiceKeys = new StorageServiceKeys();
            myStore.StorageServiceKeys.Primary = "=132321982cddsdsa";
            myStore.StorageServiceKeys.Secondary = "=w8uidjew4378891289";
            myStore.StorageServiceProperties = new StorageServiceProperties();
            myStore.StorageServiceProperties.Location = ArgumentConstants.Locations[Microsoft.PythonTools.AzureDeploymentCmdlets.Model.Location.NorthCentralUS];
            myStore.StorageServiceProperties.Status = StorageAccountStatus.Created;
            ValidStorageService.Add(myStore);

            StorageService testStore = new StorageService();
            testStore.ServiceName = "teststore";
            testStore.StorageServiceKeys = new StorageServiceKeys();
            testStore.StorageServiceKeys.Primary = "=/se23ew2343221";
            testStore.StorageServiceKeys.Secondary = "==0--3210-//121313233290sd";
            testStore.StorageServiceProperties = new StorageServiceProperties();
            testStore.StorageServiceProperties.Location = ArgumentConstants.Locations[Microsoft.PythonTools.AzureDeploymentCmdlets.Model.Location.EastAsia];
            testStore.StorageServiceProperties.Status = StorageAccountStatus.Creating;
            ValidStorageService.Add(testStore);

            StorageService MyCompanyStore = new StorageService();
            MyCompanyStore.ServiceName = "mycompanystore";
            MyCompanyStore.StorageServiceKeys = new StorageServiceKeys();
            MyCompanyStore.StorageServiceKeys.Primary = "121/21dssdsds=";
            MyCompanyStore.StorageServiceKeys.Secondary = "023432dfelfema1=";
            MyCompanyStore.StorageServiceProperties = new StorageServiceProperties();
            MyCompanyStore.StorageServiceProperties.Location = ArgumentConstants.Locations[Microsoft.PythonTools.AzureDeploymentCmdlets.Model.Location.NorthEurope];
            MyCompanyStore.StorageServiceProperties.Status = StorageAccountStatus.ResolvingDns;
            ValidStorageService.Add(MyCompanyStore);
        }

        private static void InitializeInvalidPathData()
        {
            foreach (string invalidFolderName in InvalidServiceRootName)
            {
                InvalidPath.Add(string.Format("{0}\\{1}", Directory.GetCurrentDirectory(), invalidFolderName));
            }
        }

        private static void InitializeInvalidFileNameData()
        {
            char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
            
            // Validations that depend on Path.GetFileName fails with these characters. For example:
            // if user entered name for WebRole as "My/WebRole", then Path.GetFileName get file name as WebRole.
            //
            char[] ignoreSet = { ':', '\\', '/' };

            for (int i = 0, j = 0; i < invalidFileNameChars.Length; i++, j++)
            {
                if (ignoreSet.Contains<char>(invalidFileNameChars[i]))
                {
                    continue;
                }
                j %= ValidServiceRootName.Count - 1;
                StringBuilder invalidFile = new StringBuilder(ValidServiceRootName[j]);
                invalidFile[invalidFile.Length / 2] = invalidFileNameChars[i];
                InvalidFileName.Add(invalidFile.ToString());
            }
        }

        private static void InitializeInvalidRoleInstancesData()
        {
            InvalidRoleInstances.Add(-1);
            InvalidRoleInstances.Add(-10);
            InvalidRoleInstances.Add(21);
            InvalidRoleInstances.Add(100);
        }

        private static void InitializeValidRoleInstancesData()
        {
            ValidRoleInstances.Add(1);
            ValidRoleInstances.Add(2);
            ValidRoleInstances.Add(10);
            ValidRoleInstances.Add(20);
        }

        private static void InitializeInvalidRoleNameData()
        {
            InvalidRoleName.AddRange(InvalidServiceRootName);
        }

        private static void InitializeValidRoleNameData()
        {
            ValidRoleName.Add("WebRole1");
            ValidRoleName.Add("MyWebRole");
            ValidRoleName.Add("WorkerRole");
            ValidRoleName.Add("Node_WebRole");
        }

        private static void InitializeInvalidSlotData()
        {
            InvalidSlot.Add(string.Empty);
            InvalidSlot.Add(null);
            InvalidSlot.Add("Praduction");
            InvalidSlot.Add("Pddqdww");
            InvalidSlot.Add("Stagging");
            InvalidSlot.Add("Sagiang");
        }

        private static void InitializeInvalidLocationData()
        {
            InvalidLocation.Add(string.Empty);
            InvalidLocation.Add(null);
            InvalidLocation.Add("My Home");
            InvalidLocation.Add("AnywhereUS");
            InvalidLocation.Add("USA");
            InvalidLocation.Add("Microsoft");
            InvalidLocation.Add("Near");
            InvalidLocation.Add("Anywhere Africa");
            InvalidLocation.Add("Anywhhere US");
        }

        private static void InitializeInvalidPublishSettingsData()
        {
            InvalidPublishSettings.Add(Testing.GetTestResourcePath("InvalidProfile.PublishSettings"));
        }

        private static void InitializeValidPublishSettingsData()
        {
            ValidPublishSettings.Add(Testing.GetTestResourcePath("ValidProfile.PublishSettings"));
        }

        /// <summary>
        /// This method must run after InitializeServiceRootNameData()
        /// </summary>
        private static void InitializeInvalidServiceRootNameData()
        {
            char[] invalidPathNameChars = System.IO.Path.GetInvalidPathChars();

            for (int i = 0, j = 0; i < invalidPathNameChars.Length; i++)
			{
                StringBuilder invalidPath = new StringBuilder(ValidServiceRootName[j]);
                invalidPath[invalidPath.Length / 2] = invalidPathNameChars[i];
                j %= ValidServiceRootName.Count;
                InvalidServiceRootName.Add(invalidPath.ToString());
			}
        }

        private static void InitializeValidStorageNameData()
        {
            ValidStorageName.AddRange(ValidServiceName);
        }

        private static void InitializeValidDeploymentNameData()
        {
            ValidDeploymentName.Add("MyDeployment");
            ValidDeploymentName.Add("Storage deployment");
            ValidDeploymentName.Add("_deployment name");
            ValidDeploymentName.Add("deploy service1");
        }

        private static void InitializeValidSubscriptionNameData()
        {
            ValidSubscriptionName.Add("Windows Azure Sandbox 9-220");
            ValidSubscriptionName.Add("_MySubscription");
            ValidSubscriptionName.Add("This is my subscription");
            ValidSubscriptionName.Add("Windows Azure Sandbox 284-1232");
        }

        private static void InitializeValidServiceNameData()
        {
            ValidServiceName.Add("HelloNode");
            ValidServiceName.Add("node.jsservice");
            ValidServiceName.Add("node_js_service");
            ValidServiceName.Add("node-js-service");
            ValidServiceName.Add("node-js-service123");
            ValidServiceName.Add("123node-js-service123");
            ValidServiceName.Add("123node-js2service");
        }

        private static void InitializeInvalidServiceNameData()
        {
            InvalidServiceName.Add("Hello\\Node");
            InvalidServiceName.Add("Hello/Node");
            InvalidServiceName.Add("Node App Sample");
            InvalidServiceName.Add("My$app");
            InvalidServiceName.Add("My@app");
            InvalidServiceName.Add("My#app");
            InvalidServiceName.Add("My%app");
            InvalidServiceName.Add("My^app");
            InvalidServiceName.Add("My&app");
            InvalidServiceName.Add("My*app");
            InvalidServiceName.Add("My+app");
            InvalidServiceName.Add("My=app");
            InvalidServiceName.Add("My{app");
            InvalidServiceName.Add("My}app");
            InvalidServiceName.Add("My(app");
            InvalidServiceName.Add("My)app");
            InvalidServiceName.Add("My[app");
            InvalidServiceName.Add("My]app");
            InvalidServiceName.Add("My|app");
            InvalidServiceName.Add("-MyDomain");
            InvalidServiceName.Add("MyDomain-");
            InvalidServiceName.Add("-MyDomain-");
            InvalidServiceName.Add(new string('a', 64));
        }

        private static void InitializeValidServiceRootNameData()
        {
            ValidServiceRootName.AddRange(ValidServiceName);
        }        
    }
}