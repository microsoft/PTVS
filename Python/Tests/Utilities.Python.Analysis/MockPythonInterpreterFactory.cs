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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.LegacyDB;

namespace TestUtilities.Python {
    public class MockPythonInterpreterFactory : IPythonInterpreterFactoryWithDatabase, ICustomInterpreterSerialization, IDisposable {
        readonly InterpreterConfiguration _config;
        private bool _useUpdater;
        private AnalyzerStatusUpdater _updater;
        private bool _isCurrent;
        internal bool? _success;
        public Dictionary<string, object> _properties;

        public const string UpToDateReason = "Database is up to date";
        public const string NoDatabaseReason = "Database has never been generated";
        public const string GeneratingReason = "Database is regenerating";
        public const string InvalidReason = "Database is invalid";
        public const string MissingModulesReason = "Database is missing modules";

        public MockPythonInterpreterFactory(
            InterpreterConfiguration config,
            bool withStatusUpdater = false
        ) {
            _config = config;

            _isCurrent = false;
            IsCurrentReason = NoDatabaseReason;

            _useUpdater = withStatusUpdater;
        }

        private MockPythonInterpreterFactory(Dictionary<string, object> properties) {
            _config = InterpreterConfiguration.FromDictionary(properties);

            _isCurrent = true;
            IsCurrentReason = null;

            _useUpdater = false;
        }

        public void Dispose() {
            if (_updater != null) {
                _updater.Dispose();
                _updater = null;
            }
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public IPythonInterpreter CreateInterpreter() {
            return new MockPythonInterpreter(this);
        }

        public void GenerateDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
            IsCurrentReason = GeneratingReason;
            IsCurrent = false;
            if (_useUpdater) {
                if (_updater != null) {
                    _updater.Dispose();
                }
                _updater = new AnalyzerStatusUpdater(_config.Id);
                _updater.WaitForWorkerStarted();
                _updater.ThrowPendingExceptions();
                _updater.UpdateStatus(0, 0, 0);
            }
        }

        public void NotifyNewDatabase() {
            RefreshIsCurrent();
        }

        public void EndGenerateCompletionDatabase(string id, bool success) {
            if (_updater != null) {
                for (int i = 0; i <= 100; i += 30) {
                    _updater.UpdateStatus(i, 100, 0);
                    // Need to sleep to allow the update to go through.
                    Thread.Sleep(500);
                }

                _updater.Dispose();
                _updater = null;
            }
            // Also have to sleep after disposing to make sure that the
            // completion is picked up.
            Thread.Sleep(500);

            if (success) {
                IsCurrent = true;
            } else {
                IsCurrentReason = MissingModulesReason; // won't raise an event
                IsCurrent = false;
            }
        }

        public void EndGenerateCompletionDatabase(bool success) {
            _success = success;
            NotifyNewDatabase();
        }

        public bool IsCurrent {
            get {
                return _isCurrent;
            }
            set {
                _isCurrent = value;
                if (value) {
                    IsCurrentReason = UpToDateReason;
                }
                var evt = IsCurrentChanged;
                if (evt != null) {
                    evt(this, EventArgs.Empty);
                }
                if (value) {
                    var evt2 = NewDatabaseAvailable;
                    if (evt2 != null) {
                        evt2(this, EventArgs.Empty);
                    }
                }
            }
        }

        private string IsCurrentReason { get; set; }

        public string GetAnalysisLogContent(IFormatProvider culture) {
            throw new NotImplementedException();
        }

        public event EventHandler IsCurrentChanged;

        public event EventHandler NewDatabaseAvailable;

        public void RefreshIsCurrent() {
            if (_success.HasValue) {
                IsCurrentReason = _success.Value ? UpToDateReason : MissingModulesReason;
                _isCurrent = _success.Value;
                _success = null;
            }
            var evt = IsCurrentChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public string GetFriendlyIsCurrentReason(IFormatProvider culture) {
            return IsCurrentReason;
        }

        public string GetIsCurrentReason(IFormatProvider culture) {
            return IsCurrentReason;
        }

        public bool IsCheckingDatabase {
            get { return false; }
        }

        public void NotifyCorruptDatabase() {
            IsCurrentReason = InvalidReason;
            _isCurrent = false;
        }

        public Dictionary<string, object> Properties {
            get {
                if (_properties == null) {
                    _properties = new Dictionary<string, object>();
                }
                return _properties;
            }
        }

        public object GetProperty(string propName) {
            object value = null;
            _properties?.TryGetValue(propName, out value);
            return value;
        }

        public IEnumerable<string> GetUpToDateModules() {
            yield break;
        }

        bool ICustomInterpreterSerialization.GetSerializationInfo(out string assembly, out string typeName, out Dictionary<string, object> properties) {
            assembly = GetType().Assembly.Location;
            typeName = GetType().FullName;
            properties = new Dictionary<string, object>();
            Configuration.WriteToDictionary(properties);
            return true;
        }

        public void NotifyImportNamesChanged() { }
    }
}
