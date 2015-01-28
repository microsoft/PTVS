/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;

namespace TestUtilities.Python {
    public class MockPythonInterpreterFactory : IPythonInterpreterFactoryWithDatabase, IDisposable {
        readonly InterpreterConfiguration _config;
        private bool _useUpdater;
        private AnalyzerStatusUpdater _updater;
        private bool _isCurrent;
        internal bool? _success;

        public const string UpToDateReason = "Database is up to date";
        public const string NoDatabaseReason = "Database has never been generated";
        public const string GeneratingReason = "Database is regenerating";
        public const string InvalidReason = "Database is invalid";
        public const string MissingModulesReason = "Database is missing modules";

        public MockPythonInterpreterFactory(
            Guid id,
            string description,
            InterpreterConfiguration config,
            bool withStatusUpdater = false
        ) {
            _config = config;
            Id = id;
            Description = description;

            _isCurrent = false;
            IsCurrentReason = NoDatabaseReason;

            _useUpdater = withStatusUpdater;
        }

        public void Dispose() {
            if (_updater != null) {
                _updater.Dispose();
                _updater = null;
            }
        }

        public string Description {
            get;
            private set;
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public Guid Id {
            get;
            private set;
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
                _updater = new AnalyzerStatusUpdater(AnalyzerStatusUpdater.GetIdentifier(Id, _config.Version));
                _updater.WaitForWorkerStarted();
                _updater.ThrowPendingExceptions();
                _updater.UpdateStatus(0, 0);
            }
        }

        public void NotifyNewDatabase() {
            RefreshIsCurrent();
        }

        public void EndGenerateCompletionDatabase(string id, bool success) {
            if (_updater != null) {
                for (int i = 0; i <= 100; i += 30) {
                    _updater.UpdateStatus(i, 100);
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
    }
}
