// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Globalization;
using Microsoft.DsTools.Core.Services;

namespace Microsoft.DsTools.Core.Logging {
    /// <summary>
    /// Application event logger
    /// </summary>
    public sealed class Logger : IActionLog, IDisposable {
        private readonly Lazy<IActionLogWriter[]> _logs;
        private readonly string _name;
        private readonly IActionLogWriter _writer;

        public string Folder { get; }

        public void Dispose() {
            if (_logs != null) {
                foreach (var log in _logs.Value) {
                    (log as IDisposable)?.Dispose();
                }
            }
        }

        public Logger(IActionLogWriter defaultWriter) {
            _writer = defaultWriter;
            _logs = Lazy.Create(CreateLogs);
        }

        public Logger(string name, string folder, IServiceContainer services) {
            _name = name;
            _logs = Lazy.Create(CreateLogs);
            Folder = folder;
        }

        private IActionLogWriter[] CreateLogs() {
            var logs = new IActionLogWriter[Enum.GetValues(typeof(LogVerbosity)).Length];
            logs[(int)LogVerbosity.None] = NullLogWriter.Instance;

            IActionLogWriter mainWriter = NullLogWriter.Instance;
            if (_permissions.CurrentVerbosity >= LogVerbosity.Minimal) {
                mainWriter = _writer ?? FileLogWriter.InFolder(Folder, _name);
            }

            // Unfortunately, creation of event sources in OS logs requires local admin rights.
            // http://www.christiano.ch/wordpress/2009/12/02/iis7-web-application-writing-to-event-log-generates-security-exception/
            // So we can't use OS event logs as in Dev15 there is no MSI which could elevate..
            // _maxLogLevel >= LogLevel.Minimal ? (_writer ?? new ApplicationLogWriter(_name)) : NullLogWriter.Instance;
            logs[(int)LogVerbosity.Minimal] = mainWriter;
            logs[(int)LogVerbosity.Normal] = _permissions.CurrentVerbosity >= LogVerbosity.Normal ? mainWriter : NullLogWriter.Instance;

            if (_permissions.CurrentVerbosity == LogVerbosity.Traffic) {
                logs[(int)LogVerbosity.Traffic] = _writer ?? FileLogWriter.InFolder(Folder, _name + ".traffic");
            } else {
                logs[(int)LogVerbosity.Traffic] = NullLogWriter.Instance;
            }

            return logs;
        }

        #region IActionLog
        public void Write(LogVerbosity verbosity, MessageCategory category, string message) => _logs.Value[(int)verbosity].Write(category, message);

        public void WriteFormat(LogVerbosity verbosity, MessageCategory category, string format, params object[] arguments) {
            string message = string.Format(CultureInfo.InvariantCulture, format, arguments);
            _logs.Value[(int)verbosity].Write(category, message);
        }

        public void WriteLine(LogVerbosity verbosity, MessageCategory category, string message) => _logs.Value[(int)verbosity].Write(category, message + Environment.NewLine);

        public void Flush() {
            foreach (var l in _logs.Value) {
                l?.Flush();
            }
        }

        public LogVerbosity LogVerbosity => _permissions.CurrentVerbosity;
        #endregion
    }
}
