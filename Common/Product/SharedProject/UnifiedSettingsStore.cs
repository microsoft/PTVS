// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Licensed under the Apache License, Version 2.0 (the License).

using System;
using System.Collections.Generic;
using Microsoft.Internal.VisualStudio.Services.UnifiedSettings;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;

namespace Microsoft.PythonTools.Infrastructure {
    internal sealed class UnifiedSettingsStore {
        private const SettingReadOptions ReadOptions = SettingReadOptions.RequireValidation | SettingReadOptions.NoFallbackToDefault;
        private readonly ISettingsManager _manager;
        private readonly ISettingsReader _reader;
        private readonly Func<string, SettingProvenance> _getProvenance;
        private readonly Guid _eventSource = Guid.NewGuid();

        internal UnifiedSettingsStore(ISettingsManager manager, Func<string, SettingProvenance> getProvenance) {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _reader = manager.GetReader();
            _getProvenance = getProvenance ?? throw new ArgumentNullException(nameof(getProvenance));
        }

        public static UnifiedSettingsStore Create(IServiceProvider provider) {
            var manager = provider.GetService(typeof(SVsUnifiedSettingsManager)) as ISettingsManager;
            var metadata = provider.GetService(typeof(SVsUnifiedSettingsMetadata)) as IUnifiedSettingsMetadata;
            if (manager == null || metadata == null) {
                throw new InvalidOperationException("The Visual Studio Unified Settings services are unavailable.");
            }
            return new UnifiedSettingsStore(manager, metadata.GetProvenance);
        }

        public T Read<T>(string moniker) {
            var result = _reader.GetValue<T>(moniker, ReadOptions);
            CheckRead(moniker, result.Outcome, result.Message);
            return result.Value;
        }

        public IReadOnlyList<T> ReadArray<T>(string moniker) {
            var result = _reader.GetArray<T>(moniker, ReadOptions);
            CheckRead(moniker, result.Outcome, result.Message);
            return result.Value;
        }

        public bool IsCustomized(string moniker) {
            var provenance = _getProvenance(moniker);
            if (provenance.Source == SettingValueSource.Unknown) {
                throw new InvalidOperationException($"Unable to determine the source of Unified Setting '{moniker}'.");
            }
            return provenance.IsCustomizedInDefaultScope || provenance.Source == SettingValueSource.UserCustomization;
        }

        public void Write<T>(string moniker, T value, string description) {
            var writer = _manager.GetWriter(description, _eventSource);
            var result = writer.EnqueueChange(moniker, value, SettingWriteOptions.CustomizeUnconditionally);
            Commit(writer, moniker, result, description);
        }

        public void WriteArray<T>(string moniker, IReadOnlyList<T> values, string description) {
            var writer = _manager.GetWriter(description, _eventSource);
            var result = writer.EnqueueArrayChange(moniker, values);
            Commit(writer, moniker, result, description);
        }

        public void WriteBatch(Action<ISettingsWriter> enqueueChanges, string description) {
            var writer = _manager.GetWriter(description, _eventSource);
            enqueueChanges(writer);
            Commit(writer, description);
        }

        public static void EnqueueChange<T>(ISettingsWriter writer, string moniker, T value) {
            CheckChange(moniker, writer.EnqueueChange(moniker, value, SettingWriteOptions.CustomizeUnconditionally));
        }

        public static void EnqueueArrayChange<T>(ISettingsWriter writer, string moniker, IReadOnlyList<T> values) {
            CheckChange(moniker, writer.EnqueueArrayChange(moniker, values));
        }

        public IDisposable Subscribe(Action<SettingsUpdate> handler, params string[] patterns) {
            return _reader.SubscribeToChanges(update => {
                // Reloading during our own multi-property Save would overwrite values not yet saved.
                if (update.EventSource != _eventSource) {
                    handler(update);
                }
            }, patterns);
        }

        private static void CheckRead(string moniker, SettingRetrievalOutcome outcome, string message) {
            if (outcome != SettingRetrievalOutcome.Success) {
                throw new InvalidOperationException($"Reading Unified Setting '{moniker}' failed ({outcome}): {message}");
            }
        }

        private static bool CheckChange(string moniker, SettingChangeResult change) {
            if (change.Outcome == SettingChangeOutcome.NoOp) {
                return false;
            }
            if (change.Outcome != SettingChangeOutcome.PendingCommit) {
                throw new InvalidOperationException($"Updating Unified Setting '{moniker}' failed ({change.Outcome}): {change.Message}");
            }
            return true;
        }

        private static void Commit(ISettingsWriter writer, string moniker, SettingChangeResult change, string description) {
            if (CheckChange(moniker, change)) {
                Commit(writer, description);
            }
        }

        private static void Commit(ISettingsWriter writer, string description) {
            var commit = writer.RequestCommit(description);
            if (commit.Outcome != SettingCommitOutcome.Success && commit.Outcome != SettingCommitOutcome.NoChangesQueued) {
                throw new InvalidOperationException($"Committing Unified Settings failed ({commit.Outcome}): {commit.Message}");
            }
        }
    }
}