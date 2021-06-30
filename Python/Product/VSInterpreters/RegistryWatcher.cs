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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.Win32;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides information about a detected change to the registry.
    /// </summary>
    class RegistryChangedEventArgs : EventArgs {
        /// <summary>
        /// Creates a new event object.
        /// </summary>
        /// <param name="key">The key that was originally provided.</param>
        public RegistryChangedEventArgs(RegistryHive hive,
                                        RegistryView view,
                                        string key,
                                        bool isRecursive,
                                        bool isValueChange,
                                        bool isKeyChange,
                                        object tag) {
            Hive = hive;
            View = view;
            Key = key;
            IsRecursive = isRecursive;
            IsValueChanged = isValueChange;
            IsKeyChanged = isKeyChange;
            Tag = tag;
        }

        /// <summary>
        /// The registry hive originally provided to the watcher.
        /// </summary>
        public RegistryHive Hive { get; private set; }
        /// <summary>
        /// The registry view originally provided to the watcher.
        /// </summary>
        public RegistryView View { get; private set; }
        /// <summary>
        /// The key that was originally provided to the watcher. This may not
        /// have changed to trigger the event.
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// True if the key and all its subkeys and values were being watched.
        /// </summary>
        public bool IsRecursive { get; private set; }

        /// <summary>
        /// True if the key was being watched for value changes. This may be
        /// true even if it was not a value change that triggered the event.
        /// </summary>
        public bool IsValueChanged { get; private set; }
        /// <summary>
        /// True if the key was being watched for key changes. This may be
        /// true even if it was not a key change that triggered the event.
        /// </summary>
        public bool IsKeyChanged { get; private set; }

        /// <summary>
        /// The tag that was originally provided to the watcher.
        /// </summary>
        public object Tag { get; private set; }

        /// <summary>
        /// Set to True to prevent the watcher from being run again. This is
        /// False by default.
        /// </summary>
        public bool CancelWatcher { get; set; }
    }

    /// <summary>
    /// Represents the method that will handle a registry change event.
    /// </summary>
    delegate void RegistryChangedEventHandler(object sender, RegistryChangedEventArgs e);

    /// <summary>
    /// Provides notifications when registry values are modified.
    /// </summary>
    class RegistryWatcher : IDisposable {
        readonly object _eventsLock = new object();
        readonly List<WatchEntry> _entries;
        List<RegistryWatcher> _extraWatchers;

        readonly AutoResetEvent _itemAdded;
        bool _shutdown;
        readonly Thread _thread;

        static Lazy<RegistryWatcher> _instance = new Lazy<RegistryWatcher>(() => new RegistryWatcher());
        public static RegistryWatcher Instance { get { return _instance.Value; } }

        /// <summary>
        /// Creates a new registry watcher. Each watcher will consume one CPU
        /// thread for every 64 objects.
        /// </summary>
        public RegistryWatcher() {
            _entries = new List<WatchEntry>();
            _entries.Add(new WatchEntry());
            _itemAdded = _entries[0].EventHandle;

            _thread = new Thread(Worker);
            _thread.IsBackground = true;
            _thread.Start(this);
        }

        public void Dispose() {
            if (_shutdown == false) {
                _shutdown = true;
                _itemAdded.Set();

                var extras = _extraWatchers;
                _extraWatchers = null;
                if (extras != null) {
                    foreach (var watcher in extras) {
                        watcher.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Starts listening for notifications in the specified registry key.
        /// 
        /// Each part of the key must be provided separately so that the watcher
        /// can open its own handle.
        /// </summary>
        /// <param name="hive">The hive to watch</param>
        /// <param name="view">The view to watch</param>
        /// <param name="key">The key to watch</param>
        /// <param name="handler">The event handler to invoke</param>
        /// <param name="recursive">True to watch all subkeys as well</param>
        /// <param name="notifyValueChange">
        /// True to notify if a value is added, removed or updated.
        /// </param>
        /// <param name="notifyKeyChange">
        /// True to notify if a subkey is added or removed.
        /// </param>
        /// <param name="tag">
        /// An arbitrary identifier to include with any raised events.
        /// </param>
        /// <exception cref="ArgumentException">
        /// The specified registry key does not exist.
        /// </exception>
        /// <returns>An opaque token that can be pased to Remove.</returns>
        /// <remarks>
        /// This is a thin layer over <see cref="TryAdd"/> that will throw an
        /// exception when the return value would be null.
        /// </remarks>
        public object Add(
            RegistryHive hive,
            RegistryView view,
            string key,
            RegistryChangedEventHandler handler,
            bool recursive = false,
            bool notifyValueChange = true,
            bool notifyKeyChange = true,
            object tag = null
        ) {
            var res = TryAdd(hive, view, key, handler, recursive, notifyValueChange, notifyKeyChange, tag);
            if (res == null) {
                throw new ArgumentException("Key does not exist");
            }
            return res;
        }

        /// <summary>
        /// Starts listening for notifications in the specified registry key.
        /// 
        /// Each part of the key must be provided separately so that the watcher
        /// can open its own handle.
        /// </summary>
        /// <param name="hive">The hive to watch</param>
        /// <param name="view">The view to watch</param>
        /// <param name="key">The key to watch</param>
        /// <param name="handler">The event handler to invoke</param>
        /// <param name="recursive">True to watch all subkeys as well</param>
        /// <param name="notifyValueChange">
        /// True to notify if a value is added, removed or updated.
        /// </param>
        /// <param name="notifyKeyChange">
        /// True to notify if a subkey is added or removed.
        /// </param>
        /// <param name="tag">
        /// An arbitrary identifier to include with any raised events.
        /// </param>
        /// <returns>
        /// An opaque token that can be pased to Remove, or null if the watcher
        /// could not be added.
        /// </returns>
        public object TryAdd(
            RegistryHive hive,
            RegistryView view,
            string key,
            RegistryChangedEventHandler handler,
            bool recursive = false,
            bool notifyValueChange = true,
            bool notifyKeyChange = true,
            object tag = null
        ) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }
            if (handler == null) {
                throw new ArgumentNullException("handler");
            }
            if (!(notifyValueChange | notifyKeyChange)) {
                throw new InvalidOperationException("Must wait for at least one type of change");
            }

            var args = new RegistryChangedEventArgs(
                hive,
                view,
                key,
                recursive,
                notifyValueChange,
                notifyKeyChange,
                tag
            );

            int currentWatcher = -1;
            RegistryWatcher watcher;
            bool needNewThread;
            var token = TryAddInternal(handler, args, out needNewThread);
            while (needNewThread) {
                if (_extraWatchers == null) {
                    _extraWatchers = new List<RegistryWatcher>();
                }
                currentWatcher += 1;
                if (currentWatcher >= _extraWatchers.Count) {
                    watcher = new RegistryWatcher();
                    _extraWatchers.Add(watcher);
                } else {
                    watcher = _extraWatchers[currentWatcher];
                }
                token = watcher.TryAddInternal(handler, args, out needNewThread);
            }
            return token;
        }

        private object TryAddInternal(
            RegistryChangedEventHandler handler,
            RegistryChangedEventArgs args,
            out bool needNewThread
        ) {
            WatchEntry newEntry;
            needNewThread = false;

            lock (_eventsLock) {
                if (_entries.Count >= MAXIMUM_WAIT_OBJECTS) {
                    needNewThread = true;
                    return null;
                }
                newEntry = WatchEntry.TryCreate(handler, args);
                if (newEntry == null) {
                    return null;
                }
                _entries.Add(newEntry);
            }

            _itemAdded.Set();

            return newEntry;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(object token) {
            if (token == null) {
                throw new ArgumentNullException("token");
            }
            var entry = token as WatchEntry;
            if (entry == null) {
                return false;
            }

            lock (_eventsLock) {
                if (_entries.Remove(entry)) {
                    entry.Unregister();
                    return true;
                }
            }

            var extras = _extraWatchers;
            if (extras != null) {
                foreach (var watcher in extras) {
                    if (watcher.Remove(token)) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void Worker(object param) {
            var self = (RegistryWatcher)param;

            WaitHandle[] handles;
            WatchEntry[] entries;
            int triggeredHandle;
            while (!self._shutdown) {
                lock (self._eventsLock) {
                    entries = self._entries.ToArray();
                    handles = entries.Select(e => e.EventHandle).ToArray();
                }
                triggeredHandle = WaitHandle.WaitAny(handles);

                if (triggeredHandle >= 0 && triggeredHandle < entries.Length && entries[triggeredHandle] != null) {
                    if (!entries[triggeredHandle].Invoke(self)) {
                        self.Remove(entries[triggeredHandle]);
                    }
                }
            }
        }

        private sealed class WatchEntry : IDisposable {
            private readonly AutoResetEvent _eventHandle;
            private readonly RegistryKey _key;
            private readonly RegistryChangedEventHandler _callback;
            private readonly RegistryChangedEventArgs _args;
            private bool _registered;

            /// <summary>
            /// Creates a WatchEntry that has an event but does not watch a
            /// registry key. All functions become no-ops, but
            /// <see cref="EventHandle"/> is valid.
            /// </summary>
            public WatchEntry() {
                _eventHandle = new AutoResetEvent(false);
            }

            private WatchEntry(
                RegistryKey key,
                RegistryChangedEventHandler callback,
                RegistryChangedEventArgs args
            ) {
                _key = key;
                _eventHandle = new AutoResetEvent(false);
                _callback = callback;
                _args = args;
                Register();
            }

            public static WatchEntry TryCreate(RegistryChangedEventHandler callback, RegistryChangedEventArgs args) {
                RegistryKey key;
                using (var baseKey = RegistryKey.OpenBaseKey(args.Hive, args.View)) {
                    key = baseKey.OpenSubKey(args.Key, RegistryKeyPermissionCheck.Default, RegistryRights.Notify);
                }
                if (key == null) {
                    return null;
                }
                return new WatchEntry(key, callback, args);
            }

            public void Dispose() {
                _eventHandle.Dispose();
                if (_key != null) {
                    _key.Close();
                }
            }

            public AutoResetEvent EventHandle { get { return _eventHandle; } }

            public void Register() {
                if (_key == null) {
                    return;
                }

                RegNotifyChangeKeyValue(
                    _key,
                    _eventHandle,
                    _args.IsRecursive,
                    (_args.IsValueChanged ? RegNotifyChange.Value : 0) |
                        (_args.IsKeyChanged ? RegNotifyChange.Name : 0));
                _registered = true;
            }

            public void Unregister() {
                if (_key == null) {
                    return;
                }

                if (_registered) {
                    lock (this) {
                        _registered = false;
                        _key.Close();
                    }
                }
            }

            /// <summary>
            /// Invokes the associated event handler.
            /// </summary>
            /// <returns>True if the watcher will be run again; false if the
            /// entry has been closed and can be removed.</returns>
            public bool Invoke(RegistryWatcher sender) {
                if (_key == null) {
                    // Returns true so we don't try and remove null entries from
                    // the list.
                    return true;
                }

                if (!_registered) {
                    return false;
                }

                _callback(sender, _args);

                if (_args.CancelWatcher) {
                    Unregister();
                    return false;
                }

                lock (this) {
                    // only re-register if we haven't been closed
                    if (_registered) {
                        try {
                            Register();
                        } catch (Win32Exception ex) {
                            // If we fail to re-register (probably because the
                            // key has been deleted), there's nothing that can
                            // be done. Fail if we're debugging, otherwise just
                            // continue without registering the watcher again.
                            Debug.Fail("Error registering registry watcher: " + ex.ToString());
                            _registered = false;
                        }
                    }
                }
                return true;
            }
        }

        const int MAXIMUM_WAIT_OBJECTS = 64;

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass")]
        [DllImport("advapi32", EntryPoint = "RegNotifyChangeKeyValue", CallingConvention = CallingConvention.Winapi)]
        private static extern int _RegNotifyChangeKeyValue(SafeHandle hKey, bool bWatchSubtree, RegNotifyChange dwNotifyFilter, SafeHandle hEvent, bool fAsynchronous);

        [Flags]
        enum RegNotifyChange : uint {
            Name = 1,               // REG_NOTIFY_CHANGE_NAME
            Attributes = 2,         // REG_NOTIFY_CHANGE_ATTRIBUTES
            Value = 4,              // REG_NOTIFY_CHANGE_LAST_SET
            Security = 8,           // REG_NOTIFY_CHANGE_SECURITY
            ThreadAgnostic = 0x10000000 // REG_NOTIFY_THREAD_AGNOSTIC (Windows 8 only)
        }

        static void RegNotifyChangeKeyValue(RegistryKey key, WaitHandle notifyEvent, bool recursive = false, RegNotifyChange filter = RegNotifyChange.Value) {
            int error = _RegNotifyChangeKeyValue(
                key.Handle,
                recursive,
                filter,
                notifyEvent.SafeWaitHandle,
                true);

            if (error != 0) {
                throw new Win32Exception(error);
            }
        }

        private static RegistryHive ParseRegistryKey(string key, out string subkey) {
            int firstPart = key.IndexOf('\\');
            if (firstPart < 0 || !key.StartsWithOrdinal("HKEY", ignoreCase: true)) {
                throw new ArgumentException("Invalid registry key: " + key, "key");
            }
            var hive = key.Remove(firstPart);
            subkey = key.Substring(firstPart + 1);
            if (hive.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.CurrentUser;
            } else if (hive.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.LocalMachine;
            } else if (hive.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.ClassesRoot;
            } else if (hive.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.Users;
            } else if (hive.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.CurrentConfig;
            } else if (hive.Equals("HKEY_PERFORMANCE_DATA", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.PerformanceData;
            } else if (hive.Equals("HKEY_DYN_DATA", StringComparison.OrdinalIgnoreCase)) {
                return RegistryHive.DynData;
            }
            throw new ArgumentException("Invalid registry key: " + key, "key");
        }

        internal static bool GetRegistryKeyLocation(RegistryKey key, out RegistryHive hive, out RegistryView view, out string subkey) {
            if (key != null) {
                hive = ParseRegistryKey(key.Name, out subkey);
                view = key.View;
                return true;
            } else {
                hive = RegistryHive.CurrentUser;
                view = RegistryView.Default;
                subkey = null;
                return false;
            }
        }
    }
}
