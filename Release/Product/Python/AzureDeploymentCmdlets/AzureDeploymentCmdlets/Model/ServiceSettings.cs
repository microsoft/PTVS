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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Properties;
using Microsoft.PythonTools.AzureDeploymentCmdlets.Utilities;

namespace Microsoft.PythonTools.AzureDeploymentCmdlets.Model
{
    public class ServiceSettings
    {
        /// <summary>
        /// Minimum length of a valid storage account name.
        /// http://msdn.microsoft.com/en-us/library/windowsazure/hh264518.aspx
        /// </summary>
        private const int MinimumStorageAccountNameLength = 3;

        /// <summary>
        /// Maximum length of a valid storage account name.
        /// http://msdn.microsoft.com/en-us/library/windowsazure/hh264518.aspx
        /// </summary>
        private const int MaximumStorageAccountNameLength = 24;

        // Flag indicating whether the ServiceSettings have already been loaded
        // and should begin validating any properties (which is used to allow
        // the deserializer to set empty values without tripping validation)
        private bool _shouldValidate = false;
        
        public string Slot
        {
            get { return _slot; }
            set
            {
                if (_shouldValidate)
                {
                    Validate.ValidateStringIsNullOrEmpty(value, "Slot");
                    if (!ArgumentConstants.Slots.ContainsValue(value.ToLower()))
                    {
                        throw new ArgumentException(string.Format(Resources.InvalidServiceSettingElement, "Slot"));
                    }
                }
                
                _slot = value ?? string.Empty;
            }
        }
        private string _slot = null;

        public string Location
        {
            get { return _location; }
            set
            {
                if (_shouldValidate)
                {
                    Validate.ValidateStringIsNullOrEmpty(value, "Location");
                    if (!ArgumentConstants.Locations.ContainsValue(value.ToLower()))
                    {
                        throw new ArgumentException(string.Format(Resources.InvalidServiceSettingElement, "Location"));
                    }
                }

                _location = value ?? string.Empty;
            }
        }
        private string _location = null;

        public string Subscription
        {
            get { return _subscription; }
            set
            {
                if (_shouldValidate)
                {
                    Validate.ValidateStringIsNullOrEmpty(value, "Subscription");
                }
                _subscription = value ?? string.Empty;
            }
        }
        private string _subscription = null;

        public string StorageAccountName
        {
            get { return _storageAccountName; }
            set
            {
                if (_shouldValidate)
                {
                    Validate.ValidateStringIsNullOrEmpty(value, "StorageAccountName");
                }
                _storageAccountName = value ?? string.Empty;
            }
        }
        private string _storageAccountName = null;
        
        public ServiceSettings()
        {
            _slot = string.Empty;
            _location = string.Empty;
            _subscription = string.Empty;
            _storageAccountName = string.Empty;
        }
        
        public static ServiceSettings Load(string path)
        {
            Validate.ValidateFileFull(path, Resources.ServiceSettings);

            string text = File.ReadAllText(path);
            ServiceSettings settings = new JavaScriptSerializer().Deserialize<ServiceSettings>(text);
            settings._shouldValidate = true;
            
            return settings;
        }

        public static ServiceSettings LoadDefault(string path, string slot, string location, string subscription, string storageAccountName, string suppliedServiceName, string serviceDefinitionName, out string serviceName)
        {
            ServiceSettings local;
            ServiceSettings global = new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory).GlobalSettings;
            ServiceSettings defaultServiceSettings = new ServiceSettings();

            // Load local settings if user provided local settings path
            //
            if (string.IsNullOrEmpty(path))
            {
                local = new ServiceSettings();
            }
            else
            {
                Validate.ValidateFileFull(path, Resources.ServiceSettings);
                local = Load(path);
            }

            defaultServiceSettings._slot = GetDefaultSlot(local.Slot, global.Slot, slot);
            defaultServiceSettings._location = GetDefaultLocation(local.Location, global.Location, location);
            defaultServiceSettings._subscription = GetDefaultSubscription(local.Subscription, global.Subscription, subscription);
            serviceName = GetServiceName(suppliedServiceName, serviceDefinitionName);
            defaultServiceSettings._storageAccountName = GetDefaultStorageName(local.StorageAccountName, global.StorageAccountName, storageAccountName, serviceName).ToLower();

            return defaultServiceSettings;
        }

        private static string GetServiceName(string suppliedServiceName, string serviceDefinitionName)
        {
            // If user supplied value as parameter then return it
            //
            if (!string.IsNullOrEmpty(suppliedServiceName))
            {
                return suppliedServiceName;
            }
            else
            {
                // Check to see if you have service name from *csdef
                //
                if (string.IsNullOrEmpty(serviceDefinitionName))
                {
                    // This line will be touched only if the cmd running doesn't require service name
                    //
                    return string.Empty;
                }
                else
                {
                    return serviceDefinitionName;
                }
            }
        }

        private static string GetDefaultStorageName(string localStorageName, string globalStorageAccountName, string storageAccountName, string serviceName)
        {
            Debug.Assert(serviceName != null, "serviceName cannot be null.");

            string name = serviceName;

            // If user supplied value as parameter then return it
            if (!string.IsNullOrEmpty(storageAccountName))
            {
                name = storageAccountName;
            }
            // User already has value in local service settings
            else if (!string.IsNullOrEmpty(localStorageName))
            {
                name = localStorageName;
            }
            // User already has value in global service settings
            else if (!string.IsNullOrEmpty(globalStorageAccountName))
            {
                name = globalStorageAccountName;
            }
            // If none of previous succeed, use service name as storage account name
            else if (!string.IsNullOrEmpty(serviceName))
            {
                name = SanitizeStorageAccountName(serviceName);
            }

            name = name.ToLower();
            ValidateStorageAccountName(name);

            return name;
        }

        /// <summary>
        /// Sanitize a name for use as a storage account name.
        /// </summary>
        /// <param name="name">The potential storage account name.</param>
        /// <returns>The sanitized storage account name.</returns>
        private static string SanitizeStorageAccountName(string name)
        {
            // Replace any non-letters or non-digits with their hex equivalent
            StringBuilder builder = new StringBuilder(name.Length);
            foreach (char ch in name)
            {
                if (char.IsLetter(ch) || char.IsDigit(ch))
                {
                    builder.Append(ch);
                }
                else
                {
                    builder.AppendFormat("x{0:X}", (ushort)ch);
                }
            }

            // Trim the sanitized name to at most 24 characters.
            name = builder.ToString(
                0,
                Math.Min(builder.Length, MaximumStorageAccountNameLength));

            return name;
        }

        /// <summary>
        /// Validate that the storage account name contains only lower case
        /// letters or numbers and is between 3 and 24 characters in length
        /// (per http://msdn.microsoft.com/en-us/library/windowsazure/hh264518.aspx)
        /// unless the string is empty (which can happen if it wasn't provided
        /// or generated).
        /// </summary>
        /// <param name="name">The storage account name.</param>
        private static void ValidateStorageAccountName(string name)
        {
            if (!string.IsNullOrEmpty(name) &&
                (!name.All(c => char.IsLower(c) || char.IsDigit(c)) ||
                  name.Length < MinimumStorageAccountNameLength ||
                  name.Length > MaximumStorageAccountNameLength))
            {
                throw new ArgumentException(string.Format(Properties.Resources.ServiceSettings_ValidateStorageAccountName_InvalidName, name));
            }
        }

        private static string GetDefaultSubscription(string localSubscription, string globalSubscription, string subscription)
        {
            // If user supplied value as parameter then return it
            //
            if (!string.IsNullOrEmpty(subscription))
            {
                return subscription;
            }
            // User already has value in local service settings
            //
            else if (!string.IsNullOrEmpty(localSubscription))
            {
                return localSubscription;
            }
            // User already has value in global service settings
            //
            else if (!string.IsNullOrEmpty(globalSubscription))
            {
                return globalSubscription;
            }
            // If none of previous succeed, use first entry in *PublishSettings as subscription name
            //
            else
            {
                return new GlobalComponents(GlobalPathInfo.GlobalSettingsDirectory).PublishSettings.Items[0].Subscription[0].Name;
            }
        }

        private static string GetDefaultLocation(string localLocation, string globalLocation, string location)
        {
            // If user supplied value as parameter then return it
            //
            if (!string.IsNullOrEmpty(location))
            {
                if (ArgumentConstants.Locations.ContainsValue(location.ToLower()))
                {
                    return location.ToLower();
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.InvalidServiceSettingElement, "Location"));
                };
            }
            // User already has value in local service settings
            //
            else if (!string.IsNullOrEmpty(localLocation))
            {
                return localLocation.ToLower();
            }
            // User already has value in global service settings
            //
            else if (!string.IsNullOrEmpty(globalLocation))
            {
                return globalLocation.ToLower();
            }
            // If none of previous succeed, get random location from "North Central US" or "South Central US"
            //
            else
            {
                int randomLocation = General.GetRandomFromTwo((int)Model.Location.NorthCentralUS, (int)Model.Location.SouthCentralUS);
                return ArgumentConstants.Locations[(Model.Location)randomLocation];
            }
        }

        private static string GetDefaultSlot(string localSlot, string globalSlot, string slot)
        {
            // If user supplied value as parameter then return it
            //
            if (!string.IsNullOrEmpty(slot))
            {
                if (ArgumentConstants.Slots.ContainsValue(slot.ToLower()))
                {
                    return slot.ToLower();
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.InvalidServiceSettingElement, "Slot"));
                }
            }
            // User already has value in local service settings
            //
            else if (!string.IsNullOrEmpty(localSlot))
            {
                return localSlot.ToLower();
            }
            // User already has value in global service settings
            //
            else if (!string.IsNullOrEmpty(globalSlot))
            {
                return globalSlot.ToLower();
            }
            // If none of previous succeed, use Production as default slot
            //
            else
            {
                return ArgumentConstants.Slots[Model.Slot.Production];
            }
        }
        
        public void Save(string path)
        {
            Validate.ValidateStringIsNullOrEmpty(path, Resources.ServiceSettings);
            Validate.ValidateDirectoryFull(Path.GetDirectoryName(path), Resources.ServiceSettings);

            File.WriteAllText(path, new JavaScriptSerializer().Serialize(this));
        }
        
        public override bool Equals(object obj)
        {
            ServiceSettings other = (ServiceSettings)obj;

            return
                this.Location.Equals(other.Location) &&
                this.Slot.Equals(other.Slot) &&
                this.StorageAccountName.Equals(other.StorageAccountName) &&
                this.Subscription.Equals(other.Subscription);
        }
        
        public override int GetHashCode()
        {
            return
                Location.GetHashCode() ^
                Slot.GetHashCode() ^
                StorageAccountName.GetHashCode() ^
                Subscription.GetHashCode();
        }
    }
}