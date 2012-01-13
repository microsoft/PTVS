/***************************************************************************

Copyright (c) Microsoft Corporation. All rights reserved.
This code is licensed under the Visual Studio SDK license terms.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.TC.TestHostAdapters
{
    internal static class RegistryHelper<T>
    {
        /// <summary>
        /// Get value of the specified key in registry.
        /// If the key does not exist or not enough security permissions returns default value.
        /// 'ignoring exception' means it should not throw except really bad exceptions like outofmery, clrexecution, etc.
        /// </summary>
        /// <param name="hive">Registry hive, like: Registry.LocalMachine.</param>
        /// <param name="subkeyName">The name of the subkey under hive specified by the hive parameter.</param>
        /// <param name="valueName">The name of the value. To get default key value, specify null.</param>
        /// <param name="defaultValue">The value to return when key does not exist or not enough permissions or wrong key type.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal static T GetValueIgnoringExceptions(RegistryKey hive, string subkeyName, string valueName, T defaultValue)
        {
            Debug.Assert(hive != null, "GetValueIgnoringExceptions: hive is null.");
            Debug.Assert(!string.IsNullOrEmpty(subkeyName), "GetValueIgnoringExceptions: subkeyName is null.");
            // valueName can be null or empty - this is to get default key value.

            try
            {
                using (RegistryKey key = hive.OpenSubKey(subkeyName))
                {
                    if (key != null)
                    {
                        object value = key.GetValue(valueName);
                        if (value != null)
                        {
                            return (T)value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Too many: ArgumentException, ObjectDisposedException, SecurityException, InvalidCastException, UnauthorizedAccessException
                Debug.Fail(string.Format(CultureInfo.InvariantCulture, 
                    "RegistryHelper.GetValueIgnoringExceptions: {0}", ex));
                // Ignore the exception.
            }

            return defaultValue;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        internal static void SetValueIgnoringExceptions(RegistryKey hive, string subkeyName, string valueName, T newValue)
        {
            Debug.Assert(hive != null, "GetValueIgnoringExceptions: hive is null.");
            Debug.Assert(!string.IsNullOrEmpty(subkeyName), "GetValueIgnoringExceptions: subkeyName is null.");
            // valueName can be null or empty - this is to get default key value.
            // Can't check newValue for null - it can be value type.

            try
            {
                using (RegistryKey key = hive.OpenSubKey(subkeyName, true))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, newValue);
                    }
                }
            }
            catch (Exception ex)
            {
                // Too many: ArgumentException, ObjectDisposedException, SecurityException, InvalidCastException, UnauthorizedAccessException
                Debug.Fail(string.Format(CultureInfo.InvariantCulture, 
                    "RegistryHelper.GetValueIgnoringExceptions: ", ex));
                // Ignore the exception.
            }
        }
    }
}
