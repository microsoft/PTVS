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
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
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
        /// <returns>An opaque token that can be pased to Remove.</returns>
        public object Add(RegistryHive hive,
                          RegistryView view,
                          string key,
                          RegistryChangedEventHandler handler,
                          bool recursive = false,
                          bool notifyValueChange = true,
                          bool notifyKeyChange = true,
                          object tag = null) {
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
            var token = AddInternal(handler, args);
            while (token == null) {
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
                token = watcher.AddInternal(handler, args);
            }
            return token;
        }

        private object AddInternal(RegistryChangedEventHandler handler, RegistryChangedEventArgs args) {
            WatchEntry newEntry;

            lock (_eventsLock) {
                if (_entries.Count >= MAXIMUM_WAIT_OBJECTS) {
                    return null;
                }
                newEntry = new WatchEntry(handler, args);
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

        private class WatchEntry : IDisposable {
            /// <summary>
            /// Creates a WatchEntry that has an event but does not watch a
            /// registry key. All functions become no-ops, but
            /// <see cref="EventHandle"/> is valid.
            /// </summary>
            public WatchEntry() {
                _eventHandle = new AutoResetEvent(false);
            }

            public WatchEntry(RegistryChangedEventHandler callback, RegistryChangedEventArgs args) {
                using (var baseKey = RegistryKey.OpenBaseKey(args.Hive, args.View)) {
                    _key = baseKey.OpenSubKey(args.Key, RegistryKeyPermissionCheck.Default, RegistryRights.Notify);
                }
                if (_key == null) {
                    throw new ArgumentException("Key does not exist");
                }
                _eventHandle = new AutoResetEvent(false);
                _callback = callback;
                _args = args;
                Register();
            }

            private readonly AutoResetEvent _eventHandle;
            private readonly RegistryKey _key;
            private readonly RegistryChangedEventHandler _callback;
            private readonly RegistryChangedEventArgs _args;
            private bool _registered;

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
                    _registered = false;
                    _key.Close();
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

                Register();
                return true;
            }

            public void Dispose() {
                Unregister();
            }
        }

        const int MAXIMUM_WAIT_OBJECTS = 64;

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
    }
}
