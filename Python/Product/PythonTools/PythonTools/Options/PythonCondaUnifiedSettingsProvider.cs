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

#if DEV18

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace Microsoft.PythonTools.Options {
    [Guid(ServiceGuid)]
    internal sealed class PythonCondaUnifiedSettingsProvider : IExternalSettingsProvider {
        internal const string ServiceGuid = "D23A9D09-7587-4F64-B22A-65CF5B0C51EF";
        internal const string CustomCondaExecutablePathMoniker = "customCondaExecutablePath";

        private readonly PythonToolsService _service;

        internal PythonCondaUnifiedSettingsProvider(PythonToolsService service) {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _service.CondaOptionsChanged += OnOptionsChanged;
        }

        public event EventHandler<ExternalSettingsChangedEventArgs> SettingValuesChanged;

        public event EventHandler<EnumSettingChoicesChangedEventArgs> EnumSettingChoicesChanged {
            add { }
            remove { }
        }

        public event EventHandler<DynamicMessageTextChangedEventArgs> DynamicMessageTextChanged {
            add { }
            remove { }
        }

        public event EventHandler ErrorConditionResolved {
            add { }
            remove { }
        }

        public Task<ExternalSettingOperationResult<T>> GetValueAsync<T>(string moniker, CancellationToken cancellationToken) where T : notnull {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCustomCondaExecutablePath(moniker) || typeof(T) != typeof(string)) {
                return ExternalSettingOperationResult.FailureResultTask<T>(
                    Strings.CondaUnifiedSettingsUnknownSetting,
                    ExternalSettingsErrorScope.SingleSettingOnly,
                    isTransient: false
                );
            }

            // Read through to the legacy backing store without materializing the
            // lazily cached options used by existing consumers.
            var value = _service.LoadString(
                PythonCondaOptions.CustomCondaExecutablePathSetting,
                PythonCondaOptions.Category
            ) ?? string.Empty;
            _service.RefreshCondaOptions();
            return ExternalSettingOperationResult.SuccessResultTask((T)(object)value);
        }

        public Task<ExternalSettingOperationResult> SetValueAsync<T>(string moniker, T value, CancellationToken cancellationToken) where T : notnull {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCustomCondaExecutablePath(moniker) || !(value is string path)) {
                return Task.FromResult<ExternalSettingOperationResult>(
                    new ExternalSettingOperationResult.Failure(
                        Strings.CondaUnifiedSettingsUnknownSetting,
                        ExternalSettingsErrorScope.SingleSettingOnly,
                        isTransient: false
                    )
                );
            }

            // Persist immediately, then update the shared cache only when an existing
            // consumer has already caused that lazy value to be created.
            _service.SaveString(
                PythonCondaOptions.CustomCondaExecutablePathSetting,
                PythonCondaOptions.Category,
                path
            );
            _service.RefreshCondaOptions();
            OnOptionsChanged(this, EventArgs.Empty);
            return ExternalSettingOperationResult.SuccessResultTask();
        }

        public Task<string> GetMessageTextAsync(string messageId, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);

        public Task<ExternalSettingOperationResult<IReadOnlyList<EnumChoice>>> GetEnumChoicesAsync(
            string enumSettingMoniker,
            CancellationToken cancellationToken
        ) => ExternalSettingOperationResult.SuccessResultTask<IReadOnlyList<EnumChoice>>(Array.Empty<EnumChoice>());

        public Task OpenBackingStoreAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static bool IsCustomCondaExecutablePath(string moniker)
            => string.Equals(moniker, CustomCondaExecutablePathMoniker, StringComparison.OrdinalIgnoreCase);

        private void OnOptionsChanged(object sender, EventArgs e)
            => SettingValuesChanged?.Invoke(
                this,
                ExternalSettingsChangedEventArgs.Single(CustomCondaExecutablePathMoniker)
            );
    }
}

#endif
