// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Microsoft.DsTools.Core.Logging {
    internal sealed class FileLogWriter : IActionLogWriter {
        private const int _maxTimeout = 5000;
        private static readonly ConcurrentDictionary<string, FileLogWriter> _writers = new ConcurrentDictionary<string, FileLogWriter>();

        private readonly char[] _lineBreaks = { '\n' };
        private readonly string _filePath;
        private readonly int _maxMessagesCount;
        private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private FileLogWriter(string filePath, int maxMessagesCount = 20, int timerTimeout = _maxTimeout) {
            _filePath = filePath;
            _maxMessagesCount = maxMessagesCount;
            if (timerTimeout > 0) {
                var timer = new Timer(OnTimer, null, timerTimeout, timerTimeout);
            }

            AppDomain.CurrentDomain.DomainUnload += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnTimer(object state) => StartWritingToFile();

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) 
            => WriteToFileAsync().Wait(_maxTimeout);

        private void OnProcessExit(object sender, EventArgs e) => WriteToFileAsync().Wait(_maxTimeout);

        private void StartWritingToFile() {
            if (!_messages.IsEmpty) {
                WriteToFileAsync().DoNotWait();
            }
        }

        private async Task WriteToFileAsync() {
            await _semaphore.WaitAsync();
            try {
                var sb = new StringBuilder();
                while (_messages.TryDequeue(out string message)) {
                    sb.AppendLine(message);
                }

                if (sb.Length > 0) {
                    using (var stream = File.AppendText(_filePath)) {
                        await stream.WriteAsync(sb.ToString());
                    }
                }
            } catch (UnauthorizedAccessException ex) {
                Trace.Fail(ex.ToString());
            } catch (PathTooLongException ex) {
                Trace.Fail(ex.ToString());
            } catch (DirectoryNotFoundException ex) {
                Trace.Fail(ex.ToString());
            } catch (NotSupportedException ex) {
                Trace.Fail(ex.ToString());
            } catch (IOException ex) {
                Trace.Fail(ex.ToString());
            } finally {
                _semaphore.Release();
            }
        }

        public void Write(MessageCategory category, string message) {
            var messageString = GetStringToWrite(category, message);
            _messages.Enqueue(messageString);
            if (_messages.Count > _maxMessagesCount) {
                WriteToFileAsync().DoNotWait();
            }
        }

        public void Flush() => WriteToFileAsync().Wait(_maxTimeout);

        private string GetStringToWrite(MessageCategory category, string message) {
            var categoryString = GetCategoryString(category);
            var prefix = Invariant($"[{DateTime.Now:yy-M-dd_HH-mm-ss}]{categoryString}:");
            if (!message.Take(message.Length - 1).Contains('\n')) {
                return prefix + message;
            }

            var emptyPrefix = new string(' ', prefix.Length);
            var lines = message.Split(_lineBreaks, StringSplitOptions.RemoveEmptyEntries)
                .Select((line, i) => i == 0 ? prefix + line + "\n" : emptyPrefix + line + "\n");
            return string.Concat(lines);
        }

        public static FileLogWriter InFolder(string folder, string fileName, int maxMessagesCount = 20, int autoFlushTimeout = 5000) => _writers.GetOrAdd(fileName, _ => {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, Invariant($@"{fileName}_{DateTime.Now:yyyyMdd_HHmmss}_pid{Process.GetCurrentProcess().Id}.log"));
            return new FileLogWriter(path, maxMessagesCount, autoFlushTimeout);
        });

        private static string GetCategoryString(MessageCategory category) {
            switch (category) {
                case MessageCategory.Error:
                    return "[ERROR]";
                case MessageCategory.Warning:
                    return "[WARNING]";
                default:
                    return string.Empty;
            }
        }
    }
}