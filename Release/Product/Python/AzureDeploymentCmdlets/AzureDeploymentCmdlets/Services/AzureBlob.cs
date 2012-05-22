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
    using System.IO;
    using System.ServiceModel;
    using Microsoft.WindowsAzure;
    using Microsoft.WindowsAzure.StorageClient;

    public static class AzureBlob
    {
        public static readonly string BlobEndpointTemplate = "http://{0}.blob.core.windows.net/";
        private const string containerName = "azpsnode122011";

        public static Uri UploadPackageToBlob(IServiceManagement channel, string storageName, string subscriptionId, string packagePath)
        {
            StorageService storageService = null;
            
            storageService = channel.GetStorageKeys(subscriptionId, storageName);
            string storageKey = storageService.StorageServiceKeys.Primary;

            return AzureBlob.UploadFile(storageName, storageKey, packagePath);
        }

        /// <summary>
        /// Uploads a file to azure store.
        /// </summary>
        /// <param name="storageName">Store which file will be uploaded to</param>
        /// <param name="storageKey">Store access key</param>
        /// <param name="filePath">Path to file which will be uploaded</param>
        /// <returns>Uri which holds locates the uploaded file</returns>
        /// <remarks>The uploaded file name will be guid</remarks>
        public static Uri UploadFile(string storageName, string storageKey, string filePath)
        {
            var baseAddress = string.Format(CultureInfo.InvariantCulture, AzureBlob.BlobEndpointTemplate, storageName);
            var credentials = new StorageCredentialsAccountAndKey(storageName, storageKey);
            var client = new CloudBlobClient(baseAddress, credentials);
            string blobName = Guid.NewGuid().ToString();

            CloudBlobContainer container = client.GetContainerReference(containerName);
            container.CreateIfNotExist();
            CloudBlob blob = container.GetBlobReference(blobName);
            UploadBlobStream(blob, filePath);

            return new Uri(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}{1}{2}{3}",
                    client.BaseUri,
                    containerName,
                    client.DefaultDelimiter,
                    blobName));
        }

        private static void UploadBlobStream(CloudBlob blob, string sourceFile)
        {
            using (FileStream readStream = File.OpenRead(sourceFile))
            {
                byte[] buffer = new byte[1024 * 128];

                using (BlobStream blobStream = blob.OpenWrite())
                {
                    blobStream.BlockSize = 1024 * 128;

                    while (true)
                    {
                        int bytesCount = readStream.Read(buffer, 0, buffer.Length);

                        if (bytesCount <= 0)
                        {
                            break;
                        }

                        blobStream.Write(buffer, 0, bytesCount);
                    }
                }
            }
        }

        /// <summary>
        /// Removes uploaded package from storage account.
        /// </summary>
        /// <param name="channel">Channel to use for REST calls</param>
        /// <param name="storageName">Store which has the package</param>
        /// <param name="subscriptionId">Subscription which has the store</param>
        public static void RemovePackageFromBlob(IServiceManagement channel, string storageName, string subscriptionId)
        {
            StorageService storageService = null;
            
            storageService = channel.GetStorageKeys(subscriptionId, storageName);
            string storageKey = storageService.StorageServiceKeys.Primary;

            AzureBlob.RemoveFile(storageName, storageKey);
        }

        /// <summary>
        /// Removes file from storage account
        /// </summary>
        /// <param name="storageName">Store which has file to remove</param>
        /// <param name="storageKey">Store access key</param>
        private static void RemoveFile(string storageName, string storageKey)
        {
            var baseAddress = string.Format(CultureInfo.InvariantCulture, AzureBlob.BlobEndpointTemplate, storageName);
            var credentials = new StorageCredentialsAccountAndKey(storageName, storageKey);
            var client = new CloudBlobClient(baseAddress, credentials);

            CloudBlobContainer container = client.GetContainerReference(containerName);
            if (Exists(container))
            {
                container.Delete();
            }
        }

        /// <summary>
        /// Checks if a container exists.
        /// </summary>
        /// <param name="container">Container to check for</param>
        /// <returns>Flag indicating the existence of the container</returns>
        private static bool Exists(CloudBlobContainer container)
        {
            try
            {
                container.FetchAttributes();
                return true;
            }
            catch (StorageClientException e)
            {
                if (e.ErrorCode == StorageErrorCode.ResourceNotFound)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }
    }
}