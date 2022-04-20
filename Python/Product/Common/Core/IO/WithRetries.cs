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
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.Logging;

namespace Microsoft.PythonTools.Common.Core.IO {
    public static class WithRetries {
        public static T Execute<T>(Func<T> a, string errorMessage, ILogger log) {
            Exception ex = null;
            for (var retries = 50; retries > 0; --retries) {
                try {
                    return a();
                } catch (Exception ex1) when (ex1 is IOException || ex1 is UnauthorizedAccessException) {
                    ex = ex1;
                    Thread.Sleep(10);
                } catch (Exception ex2) {
                    ex = ex2;
                    break;
                }
            }
            if (ex != null) {
                log?.Log(TraceEventType.Warning, $"{errorMessage} Exception: {ex.Message}");
                if (ex.IsCriticalException()) {
                    throw ex;
                }
            }
            return default;
        }
    }
}
