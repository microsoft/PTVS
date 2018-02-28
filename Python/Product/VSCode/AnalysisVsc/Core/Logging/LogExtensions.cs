// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.DsTools.Core.Disposables;
using static System.FormattableString;

namespace Microsoft.DsTools.Core.Logging {
    public static class LogExtensions {
        public static IDisposable Measure(this IActionLog log, string message) {
            log.Write(MessageCategory.General, Invariant($"{message} started"));
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            return Disposable.Create(() => {
                stopwatch.Stop();
                log.Write(MessageCategory.General, Invariant($"{message} completed in {stopwatch.ElapsedMilliseconds} ms."));
            });
        }
    }
}
