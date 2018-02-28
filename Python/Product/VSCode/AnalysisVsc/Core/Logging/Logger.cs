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
        private readonly string _name;
        private readonly Lazy<IActionLogWriter> _log;
        private readonly IActionLogWriter _writer;

        public string Folder { get; }

        public void Dispose() => (_log as IDisposable)?.Dispose();

        public Logger(IActionLogWriter defaultWriter) {
            _writer = defaultWriter;
            _log = Lazy.Create(CreateLog);
        }

        public Logger(string name, string folder, IServiceContainer services) {
            _name = name;
            _log = Lazy.Create(CreateLog);
            Folder = folder;
        }

        private IActionLogWriter CreateLog() {
            return _writer ?? FileLogWriter.InFolder(Folder, _name);
        }

        #region IActionLog
        public void Write(MessageCategory category, string message) => _log.Value.Write(category, message);

        public void WriteFormat(MessageCategory category, string format, params object[] arguments) {
            string message = string.Format(CultureInfo.InvariantCulture, format, arguments);
            _log.Value.Write(category, message);
        }

        public void WriteLine(MessageCategory category, string message) => _log.Value.Write(category, message + Environment.NewLine);

        public void Flush() => _log.Value.Flush();
        #endregion
    }
}
