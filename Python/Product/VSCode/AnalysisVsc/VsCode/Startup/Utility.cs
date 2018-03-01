// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.PythonTools.VsCode.Startup {
    internal static class Utility {
        public static readonly JsonSerializer CamelCaseJsonSerializer = new JsonSerializer {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }
}
