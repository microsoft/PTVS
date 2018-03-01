// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.PythonTools.VsCode.Core.Shell {
    public interface ITelemetryService {
        Task SendTelemetry(object o);
    }
}
