// Python Tools for Visual Studio
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Interpreter {
    interface ICondaEnvironmentManager {
        /// <summary>
        /// Creates a new conda environment and installs the specified conda
        /// packages.
        /// </summary>
        /// <param name="newEnvNameOrPath">
        /// Name or absolute path of folder for the new environment. If a name
        /// or relative path is used, it will be created in the user's default
        /// environments folder, as determined by conda.
        /// </param>
        /// <param name="packageSpecs">
        /// List of conda packages to install.
        /// </param>
        Task<bool> CreateAsync(string newEnvNameOrPath, IEnumerable<PackageSpec> packageSpecs, ICondaEnvironmentManagerUI ui, CancellationToken ct);

        /// <summary>
        /// Previews the creation of a new conda environment with the specified
        /// conda packages. No environment is created.
        /// </summary>
        /// <param name="newEnvNameOrPath">
        /// Name or absolute path of folder for the new environment. If a name
        /// or relative path is used, it will be created in the user's default
        /// environments folder, as determined by conda.
        /// </param>
        /// <param name="packageSpecs">
        /// List of conda packages to install.
        /// </param>
        Task<CondaCreateDryRunResult> PreviewCreateAsync(string newEnvNameOrPath, IEnumerable<PackageSpec> packageSpecs, CancellationToken ct);

        /// <summary>
        /// Creates a new conda environment and installs the conda and pip
        /// packages as specified in the environment file.
        /// </summary>
        /// <param name="newEnvNameOrPath">
        /// Name or absolute path of folder for the new environment. If a name
        /// or relative path is used, it will be created in the user's default
        /// environments folder, as determined by conda.
        /// </param>
        /// <param name="sourceEnvFilePath">
        /// Path to the environment (yaml) file to import.
        /// </param>
        Task<bool> CreateFromEnvironmentFileAsync(string newEnvNameOrPath, string sourceEnvFilePath, ICondaEnvironmentManagerUI ui, CancellationToken ct);


        /// <summary>
        /// Creates a new conda environment by cloning an existing environment.
        /// </summary>
        /// <param name="newEnvNameOrPath">
        /// Name or absolute path of folder for the new environment. If a name
        /// or relative path is used, it will be created in the user's default
        /// environments folder, as determined by conda.
        /// </param>
        /// <param name="sourceEnvPath">
        /// Path to the existing environment to clone.
        /// </param>
        Task<bool> CreateFromExistingEnvironmentAsync(string newEnvNameOrPath, string sourceEnvPath, ICondaEnvironmentManagerUI ui, CancellationToken ct);

        /// <summary>
        /// Exports the list of conda and pip packages with versions to an
        /// environment file that can be used to reproduce an environment.
        /// </summary>
        /// <param name="envPath">
        /// Path to the existing environment to export.
        /// </param>
        /// <param name="destinationEnvFilePath">
        /// Path to the environment file to create.
        /// </param>
        Task<bool> ExportEnvironmentFileAsync(string envPath, string destinationEnvFilePath, ICondaEnvironmentManagerUI ui, CancellationToken ct);

        /// <summary>
        /// Exports the list of conda packages with platform specific URLs
        /// to the packages to an explicit specification file that can be
        /// used to build an identical environment on the same operating
        /// system, on the same or different machine.
        /// </summary>
        /// <param name="envPath">
        /// Path to the existing environment to export.
        /// </param>
        /// <param name="destinationSpecFilePath">
        /// Path to the explicit specification file to create.
        /// </param>
        Task<bool> ExportExplicitSpecificationFileAsync(string envPath, string destinationSpecFilePath, ICondaEnvironmentManagerUI ui, CancellationToken ct);

        /// <summary>
        /// Deletes an existing environment from disk.
        /// </summary>
        /// <param name="envPath">
        /// Path to the existing environment to delete.
        /// </param>
        Task<bool> DeleteAsync(string envPath, ICondaEnvironmentManagerUI ui, CancellationToken ct);
    }

    sealed class CondaCreateDryRunResult {
        [JsonProperty("actions")]
        public CondaCreateDryRunActions Actions = null;

        [JsonProperty("prefix")]
        public string PrefixFolder = null;

        [JsonProperty("error")]
        public string Error = null;

        [JsonProperty("message")]
        public string Message = null;

        [JsonProperty("success")]
        public bool Success;
    }

    sealed class CondaCreateDryRunActions {
        [JsonProperty("FETCH")]
        public CondaCreateDryRunPackage[] FetchPackages = null;

        [JsonProperty("LINK")]
        public CondaCreateDryRunPackage[] LinkPackages = null;

        [JsonProperty("PREFIX")]
        public string PrefixFolder = null;
    }

    sealed class CondaCreateDryRunPackage {
        [JsonProperty("name")]
        public string Name = null;

        [JsonProperty("version")]
        public string VersionText = null;
    }
}
