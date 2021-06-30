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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace TestUtilities.Mocks
{
#pragma warning disable CS0246 // The type or namespace name 'IVsActivityLog' could not be found (are you missing a using directive or an assembly reference?)
    public class MockActivityLog : IVsActivityLog
#pragma warning restore CS0246 // The type or namespace name 'IVsActivityLog' could not be found (are you missing a using directive or an assembly reference?)
    {
        public readonly List<string> Items = new List<string>();

        private readonly static Dictionary<uint, string> ActivityType = new Dictionary<uint, string> {
            { (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, "Error" },
            { (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_WARNING, "Warning" },
            { (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, "Information" }
        };

        public IEnumerable<string> AllItems
        {
            get
            {
                return Items.Select(t => Regex.Replace(t, "(\\r\\n|\\r|\\n)", "\\n"));
            }
        }

        public IEnumerable<string> Errors
        {
            get
            {
                return Items
                    .Where(t => t.StartsWith("Error"))
                    .Select(t => Regex.Replace(t, "(\\r\\n|\\r|\\n)", "\\n"));
            }
        }

        public IEnumerable<string> ErrorsAndWarnings
        {
            get
            {
                return Items
                    .Where(t => t.StartsWith("Error") || t.StartsWith("Warning"))
                    .Select(t => Regex.Replace(t, "(\\r\\n|\\r|\\n)", "\\n"));
            }
        }

        public int LogEntry(uint actType, string pszSource, string pszDescription)
        {
            var item = string.Format("{0}//{1}//{2}", ActivityType[actType], pszSource, pszDescription);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryGuid(uint actType, string pszSource, string pszDescription, Guid guid)
        {
            var item = string.Format("{0}//{1}//{2}//{3:B}", ActivityType[actType], pszSource, pszDescription, guid);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryGuidHr(uint actType, string pszSource, string pszDescription, Guid guid, int hr)
        {
            var item = string.Format("{0}//{1}//{2}//{3:B}//{4:X8}", ActivityType[actType], pszSource, pszDescription, guid, hr);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryGuidHrPath(uint actType, string pszSource, string pszDescription, Guid guid, int hr, string pszPath)
        {
            var item = string.Format("{0}//{1}//{2}//{3:B}//{4:X8}//{5}", ActivityType[actType], pszSource, pszDescription, guid, hr, pszPath);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryGuidPath(uint actType, string pszSource, string pszDescription, Guid guid, string pszPath)
        {
            var item = string.Format("{0}//{1}//{2}//{3:B}//{4}", ActivityType[actType], pszSource, pszDescription, guid, pszPath);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryHr(uint actType, string pszSource, string pszDescription, int hr)
        {
            var item = string.Format("{0}//{1}//{2}//{3:X8}", ActivityType[actType], pszSource, pszDescription, hr);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryHrPath(uint actType, string pszSource, string pszDescription, int hr, string pszPath)
        {
            var item = string.Format("{0}//{1}//{2}//{3:X8}//{4}", ActivityType[actType], pszSource, pszDescription, hr, pszPath);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }

        public int LogEntryPath(uint actType, string pszSource, string pszDescription, string pszPath)
        {
            var item = string.Format("{0}//{1}//{2}//{3}", ActivityType[actType], pszSource, pszDescription, pszPath);
            Debug.WriteLine(item);
            Items.Add(item);
            return VSConstants.S_OK;
        }
    }
}
