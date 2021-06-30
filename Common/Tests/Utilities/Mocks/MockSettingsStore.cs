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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestUtilities.Mocks
{
#pragma warning disable CS0012 // The type 'SYSTEMTIME' is defined in an assembly that is not referenced. You must add a reference to assembly 'Microsoft.VisualStudio.Shell.Interop.8.0, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
#pragma warning disable CS0012 // The type 'SYSTEMTIME' is defined in an assembly that is not referenced. You must add a reference to assembly 'Microsoft.VisualStudio.Shell.Interop.8.0, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
    public class MockSettingsStore : IVsSettingsStore, IVsWritableSettingsStore
#pragma warning restore CS0012 // The type 'SYSTEMTIME' is defined in an assembly that is not referenced. You must add a reference to assembly 'Microsoft.VisualStudio.Shell.Interop.8.0, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
#pragma warning restore CS0012 // The type 'SYSTEMTIME' is defined in an assembly that is not referenced. You must add a reference to assembly 'Microsoft.VisualStudio.Shell.Interop.8.0, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
    {
        public bool AllowEmptyCollections { get; set; }

        private readonly List<Tuple<string, string, object>> Settings = new List<Tuple<string, string, object>>();

        public void AddSetting(string path, string name, object value)
        {
            lock (Settings)
            {
                if (string.IsNullOrEmpty(path))
                {
                    path = Settings.Last().Item1;
                }
                else if (path.StartsWith(" "))
                {
                    path = Settings.Last().Item1 + "\\" + path.TrimStart();
                }

                Settings.Add(Tuple.Create(path, name, value));
            }
        }

        public void Clear()
        {
            lock (Settings)
            {
                Settings.Clear();
            }
        }

        private int FindValue<T>(string collectionPath, string propertyName, ref T value)
        {
            lock (Settings)
            {
                var tuple = Settings.FirstOrDefault(t => t.Item1 == collectionPath && t.Item2 == propertyName);
                if (tuple == null)
                {
                    return VSConstants.S_FALSE;
                }
                if (!(tuple.Item3 is T))
                {
                    return VSConstants.E_INVALIDARG;
                }
                value = (T)tuple.Item3;
                return VSConstants.S_OK;
            }
        }

        public int CollectionExists(string collectionPath, out int pfExists)
        {
            lock (Settings)
            {
                var collectionSeq = collectionPath.Split('\\');
                pfExists = Settings
                    .Select(t => t.Item1.Split('\\'))
                    .Where(seq => seq.Length >= collectionSeq.Length)
                    .Any(seq => seq.Take(collectionSeq.Length).SequenceEqual(collectionSeq)) ? 1 : 0;
                return VSConstants.S_OK;
            }
        }

        public int GetBinary(string collectionPath, string propertyName, uint byteLength, byte[] pBytes = null, uint[] actualByteLength = null)
        {
            throw new NotImplementedException();
        }

        public int GetBool(string collectionPath, string propertyName, out int value)
        {
            return GetBoolOrDefault(collectionPath, propertyName, 0, out value);
        }

        public int GetBoolOrDefault(string collectionPath, string propertyName, int defaultValue, out int value)
        {
            bool res = (defaultValue != 0);
            var hr = FindValue(collectionPath, propertyName, ref res);
            value = res ? 1 : 0;
            return hr;
        }

        public int GetInt(string collectionPath, string propertyName, out int value)
        {
            value = 0;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetInt64(string collectionPath, string propertyName, out long value)
        {
            value = 0;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetInt64OrDefault(string collectionPath, string propertyName, long defaultValue, out long value)
        {
            value = defaultValue;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetIntOrDefault(string collectionPath, string propertyName, int defaultValue, out int value)
        {
            value = defaultValue;
            return FindValue(collectionPath, propertyName, ref value);
        }

#pragma warning disable CS0246 // The type or namespace name 'SYSTEMTIME' could not be found (are you missing a using directive or an assembly reference?)
        public int GetLastWriteTime(string collectionPath, SYSTEMTIME[] lastWriteTime)
#pragma warning restore CS0246 // The type or namespace name 'SYSTEMTIME' could not be found (are you missing a using directive or an assembly reference?)
        {
            throw new NotImplementedException();
        }

        public int GetPropertyCount(string collectionPath, out uint propertyCount)
        {
            lock (Settings)
            {
                propertyCount = (uint)Settings.Count(t => t.Item1 == collectionPath);
            }
            return VSConstants.S_OK;
        }

        public int GetPropertyName(string collectionPath, uint index, out string propertyName)
        {
            try
            {
                lock (Settings)
                {
                    propertyName = Settings.Where(t => t.Item1 == collectionPath).ElementAt((int)index).Item2;
                }
                return VSConstants.S_OK;
            }
            catch (InvalidOperationException)
            {
                propertyName = null;
                return VSConstants.E_INVALIDARG;
            }
        }

        public int GetPropertyType(string collectionPath, string propertyName, out uint type)
        {
            throw new NotImplementedException();
        }

        public int GetString(string collectionPath, string propertyName, out string value)
        {
            value = null;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetStringOrDefault(string collectionPath, string propertyName, string defaultValue, out string value)
        {
            value = defaultValue;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetSubCollectionCount(string collectionPath, out uint subCollectionCount)
        {
            var collectionSeq = collectionPath.Split('\\');
            lock (Settings)
            {
                if (!Settings
                    .Select(t => t.Item1.Split('\\'))
                    .Where(seq => seq.Length >= collectionSeq.Length)
                    .Where(seq => seq.Take(collectionSeq.Length).SequenceEqual(collectionSeq))
                    .Any())
                {
                    subCollectionCount = 0;
                    return AllowEmptyCollections ? VSConstants.S_OK : VSConstants.E_INVALIDARG;
                }

                subCollectionCount = (uint)Settings
                    .Select(t => t.Item1.Split('\\'))
                    .Where(seq => seq.Length > collectionSeq.Length)
                    .Where(seq => seq.Take(collectionSeq.Length).SequenceEqual(collectionSeq))
                    .Select(seq => seq[collectionSeq.Length])
                    .Distinct()
                    .Count();
                return VSConstants.S_OK;
            }
        }

        public int GetSubCollectionName(string collectionPath, uint index, out string subCollectionName)
        {
            var collectionSeq = collectionPath.Split('\\');
            lock (Settings)
            {
                if (!Settings
                    .Select(t => t.Item1.Split('\\'))
                    .Where(seq => seq.Length >= collectionSeq.Length)
                    .Where(seq => seq.Take(collectionSeq.Length).SequenceEqual(collectionSeq))
                    .Any())
                {
                    subCollectionName = null;
                    return VSConstants.E_INVALIDARG;
                }

                subCollectionName = Settings
                    .Select(t => t.Item1.Split('\\'))
                    .Where(seq => seq.Length > collectionSeq.Length)
                    .Where(seq => seq.Take(collectionSeq.Length).SequenceEqual(collectionSeq))
                    .Select(seq => seq[collectionSeq.Length])
                    .Distinct()
                    .ElementAt((int)index);
                return VSConstants.S_OK;
            }
        }

        public int GetUnsignedInt(string collectionPath, string propertyName, out uint value)
        {
            value = 0;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetUnsignedInt64(string collectionPath, string propertyName, out ulong value)
        {
            value = 0;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetUnsignedInt64OrDefault(string collectionPath, string propertyName, ulong defaultValue, out ulong value)
        {
            value = defaultValue;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int GetUnsignedIntOrDefault(string collectionPath, string propertyName, uint defaultValue, out uint value)
        {
            value = defaultValue;
            return FindValue(collectionPath, propertyName, ref value);
        }

        public int PropertyExists(string collectionPath, string propertyName, out int pfExists)
        {
            lock (Settings)
            {
                pfExists = Settings.Any(t => t.Item1 == collectionPath && t.Item2 == propertyName) ? 1 : 0;
            }
            return VSConstants.S_OK;
        }


        public int CreateCollection(string collectionPath)
        {
            int exists;
            int hr = CollectionExists(collectionPath, out exists);
            if (ErrorHandler.Failed(hr))
            {
                return hr;
            }
            if (exists == 0)
            {
                AddSetting(collectionPath, string.Empty, null);
            }
            return VSConstants.S_OK;
        }

        public int DeleteCollection(string collectionPath)
        {
            if (string.IsNullOrEmpty(collectionPath))
            {
                return VSConstants.S_FALSE;
            }

            var collectionSeq = collectionPath.Split('\\');
            lock (Settings)
            {
                return Settings.RemoveAll(t =>
                {
                    var seq = t.Item1.Split('\\');
                    return seq.Length >= collectionSeq.Length &&
                        seq.Take(collectionSeq.Length).SequenceEqual(collectionSeq);
                }) > 0 ? VSConstants.S_OK : VSConstants.S_FALSE;
            }
        }

        public int DeleteProperty(string collectionPath, string propertyName)
        {
            if (string.IsNullOrEmpty(collectionPath) || string.IsNullOrEmpty(propertyName))
            {
                return VSConstants.S_FALSE;
            }

            var collectionSeq = collectionPath.Split('\\');
            lock (Settings)
            {
                return Settings.RemoveAll(t => t.Item1 == collectionPath && t.Item2 == propertyName) > 0 ?
                    VSConstants.S_OK :
                    VSConstants.S_FALSE;
            }
        }

        public int SetBinary(string collectionPath, string propertyName, uint byteLength, byte[] pBytes)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)pBytes));
            }
            return VSConstants.S_OK;
        }

        public int SetBool(string collectionPath, string propertyName, int value)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)value));
            }
            return VSConstants.S_OK;
        }

        public int SetInt(string collectionPath, string propertyName, int value)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)value));
            }
            return VSConstants.S_OK;
        }

        public int SetInt64(string collectionPath, string propertyName, long value)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)value));
            }
            return VSConstants.S_OK;
        }

        public int SetString(string collectionPath, string propertyName, string value)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)value));
            }
            return VSConstants.S_OK;
        }

        public int SetUnsignedInt(string collectionPath, string propertyName, uint value)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)value));
            }
            return VSConstants.S_OK;
        }

        public int SetUnsignedInt64(string collectionPath, string propertyName, ulong value)
        {
            if (DeleteProperty(collectionPath, propertyName) == VSConstants.S_FALSE)
            {
                int exists;
                if (ErrorHandler.Failed(CollectionExists(collectionPath, out exists)) || exists == 0)
                {
                    return VSConstants.E_INVALIDARG;
                }
            }
            lock (Settings)
            {
                Settings.Add(Tuple.Create(collectionPath, propertyName, (object)value));
            }
            return VSConstants.S_OK;
        }
    }
}
