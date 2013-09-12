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
    public class MockPythonInterpreterFactory : IPythonInterpreterFactoryWithDatabase {
        readonly InterpreterConfiguration _config;
        private bool _isCurrent;
        internal bool? _success;

        public const string UpToDateReason = "Database is up to date";
        public const string NoDatabaseReason = "Database has never been generated";
        public const string GeneratingReason = "Database is regenerating";
        public const string InvalidReason = "Database is invalid";
        public const string MissingModulesReason = "Database is missing modules";

        public MockPythonInterpreterFactory(Guid id, string description, InterpreterConfiguration config) {
            _config = config;
            Id = id;
            Description = description;

            _isCurrent = false;
            IsCurrentReason = NoDatabaseReason;
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
        }

        public void NotifyNewDatabase() {
            RefreshIsCurrent();
        }

        public void EndGenerateCompletionDatabase(string id, bool success) {
            using (var updater = new AnalyzerStatusUpdater(id)) {
                for (int i = 0; i <= 100; i += 30) {
                    updater.UpdateStatus(i, 100);
                    // Need to sleep to allow the update to go through.
                    Thread.Sleep(500);
                }
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
