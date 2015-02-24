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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.EnvironmentsList {
    class PipPackageCache : IDisposable {
        private static readonly Dictionary<string, PipPackageCache> _knownCaches =
            new Dictionary<string, PipPackageCache>();
        private static readonly object _knownCachesLock = new object();

        private readonly Uri _index;
        private readonly string _indexName;
        private readonly string _cachePath;

        protected readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
        protected readonly Dictionary<string, PipPackageView> _cache;
        protected DateTime _cacheAge;

        protected int _userCount; // protected by _knownCachesLock
        private long _writeVersion;

        protected bool _isDisposed;

        private readonly static Regex IndexNameSanitizerRegex = new Regex(@"\W");
        private static readonly Regex SimpleListRegex = new Regex(@"a href=['""](?<package>[^'""]+)");


        // These constants are substituted where necessary, but are not stored
        // in instance variables so we can differentiate between set and unset.
        private static readonly Uri DefaultIndex = new Uri("https://pypi.python.org/pypi/");
        private const string DefaultIndexName = "PyPI";

        protected PipPackageCache(
            Uri index,
            string indexName,
            string cachePath
        ) {
            _index = index;
            _indexName = indexName;
            _cachePath = cachePath;
            _cache = new Dictionary<string, PipPackageView>();
        }

        public static PipPackageCache GetCache(Uri index = null, string indexName = null) {
            PipPackageCache cache;
            var key = (index ?? DefaultIndex).AbsoluteUri;
            lock (_knownCachesLock) {
                if (!_knownCaches.TryGetValue(key, out cache)) {
                    _knownCaches[key] = cache = new PipPackageCache(
                        index,
                        indexName,
                        GetCachePath(index ?? DefaultIndex, indexName ?? DefaultIndexName)
                    );
                }
                cache._userCount += 1;
            }

            return cache;
        }

        ~PipPackageCache() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;
            
            if (disposing) {
                lock (_knownCachesLock) {
                    if (--_userCount <= 0) {
                        Debug.Assert(_userCount == 0);
                        _cacheLock.Dispose();
                        _knownCaches.Remove((_index ?? DefaultIndex).AbsoluteUri);
                    }
                }
            }
        }

        public Uri Index {
            get { return _index ?? DefaultIndex; }
        }

        public string IndexName {
            get { return _indexName ?? DefaultIndexName; }
        }

        public bool HasExplicitIndex {
            get { return _index != null; }
        }


        public async Task<IList<PipPackageView>> GetAllPackagesAsync(CancellationToken cancel) {
            await _cacheLock.WaitAsync(cancel).ConfigureAwait(false);
            try {
                if (!_cache.Any()) {
                    try {
                        await ReadCacheFromDiskAsync(cancel).ConfigureAwait(false);
                    } catch (IOException) {
                        _cacheAge = DateTime.MinValue;
                    }
                }
                if (_cacheAge.AddHours(6) < DateTime.Now) {
                    await RefreshCacheAsync(cancel).ConfigureAwait(false);
                }

                return _cache.Values.ToList();
            } finally {
                _cacheLock.Release();
            }
        }

        public async Task UpdatePackageInfoAsync(PipPackageView package, CancellationToken cancel) {
            string description = null;
            List<string> versions = null;

            using (var client = new WebClient()) {
                var data = await client.OpenReadTaskAsync(new Uri(_index ?? DefaultIndex, package.Name + "/json"));

                try {
                    using (var reader = JsonReaderWriterFactory.CreateJsonReader(data, new XmlDictionaryReaderQuotas())) {
                        var doc = XDocument.Load(reader);

                        description = (string)doc.Document
                            .Elements("root")
                            .Elements("info")
                            .Elements("description")
                            .FirstOrDefault();
                        
                        versions = doc.Document
                            .Elements("root")
                            .Elements("releases")
                            .Elements()
                            .Attributes("item")
                            .Select(a => a.Value)
                            .ToList();
                    }
                } catch (InvalidOperationException) {
                }
            }

            cancel.ThrowIfCancellationRequested();

            bool changed = false;

            await _cacheLock.WaitAsync();
            try {
                PipPackageView inCache;
                if (!_cache.TryGetValue(package.Name, out inCache)) {
                    inCache = _cache[package.Name] = new PipPackageView(this, package.Name, null, null);
                }

                if (!string.IsNullOrEmpty(description)) {
                    inCache.Description = description;
                    package.Description = description;
                    changed = true;
                }

                if (versions != null) {
                    var updateVersion = Pep440Version.TryParseAll(versions)
                        .Where(v => v.IsFinalRelease)
                        .OrderByDescending(v => v)
                        .FirstOrDefault();
                    inCache.UpgradeVersion = updateVersion;
                    package.UpgradeVersion = updateVersion;
                    changed = true;
                }
            } finally {
                _cacheLock.Release();
            }

            if (changed) {
                TriggerWriteCacheToDisk();
            }
        }

        #region Cache File Management

        private static async Task<IDisposable> LockFile(string filename, CancellationToken cancel) {
            FileStream stream = null;

            while (stream == null) {
                cancel.ThrowIfCancellationRequested();
                if (!File.Exists(filename)) {
                    try {
                        stream = new FileStream(
                            filename,
                            FileMode.CreateNew,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            8,
                            FileOptions.DeleteOnClose
                        );
                    } catch (DirectoryNotFoundException) {
                        throw;
                    } catch (IOException) {
                    }
                }
                await Task.Delay(100);
            }

            return stream;
        }

        private async Task RefreshCacheAsync(CancellationToken cancel) {
            Debug.Assert(_cacheLock.CurrentCount == 0, "Cache must be locked before calling RefreshCacheAsync");

            var client = new WebClient();
            // ../simple is a list of <a href="package">package</a>
            var htmlList = await client.DownloadStringTaskAsync(
                new Uri(_index ?? DefaultIndex, "../simple")
            ).ConfigureAwait(false);

            bool changed = false;
            var toRemove = new HashSet<string>(_cache.Keys);

            // We only want to add new packages so we don't blow away
            // existing package specs in the cache.
            foreach (Match match in SimpleListRegex.Matches(htmlList)) {
                var package = match.Groups["package"].Value;
                if (string.IsNullOrEmpty(package)) {
                    continue;
                }

                if (!toRemove.Remove(package)) {
                    try {
                        _cache[package] = new PipPackageView(this, package);
                        changed = true;
                    } catch (FormatException) {
                    }
                }
            }

            foreach (var package in toRemove) {
                _cache.Remove(package);
                changed = true;
            }

            if (changed) {
                TriggerWriteCacheToDisk();
            }

            _cacheAge = DateTime.Now;
        }

        private async void TriggerWriteCacheToDisk() {
            var version = Interlocked.Increment(ref _writeVersion);
            await Task.Delay(1000).ConfigureAwait(false);
            if (Volatile.Read(ref _writeVersion) != version) {
                return;
            }

            try {
                await _cacheLock.WaitAsync();
                try {
                    await WriteCacheToDiskAsync(CancellationToken.None);
                } finally {
                    _cacheLock.Release();
                }
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
                Debug.Fail("Unhandled exception: " + ex.ToString());
                // Nowhere else to report the exception, so just swallow it to
                // avoid bringing down the whole process.
            }
        }

        protected async Task WriteCacheToDiskAsync(CancellationToken cancel) {
            Debug.Assert(_cacheLock.CurrentCount == 0, "Cache must be locked before calling WriteCacheToDiskAsync");

            try {
                using (await LockFile(_cachePath + ".lock", cancel))
                using (var file = new StreamWriter(_cachePath, false, Encoding.UTF8)) {
                    foreach (var keyValue in _cache) {
                        cancel.ThrowIfCancellationRequested();
                        await file.WriteLineAsync(keyValue.Value.GetPackageSpec(true, true));
                    }
                }
            } catch (IOException ex) {
                Debug.Fail("Failed to write cache file: " + ex.ToString());
                // Otherwise, just keep the cache in memory.
            }
        }

        protected async Task ReadCacheFromDiskAsync(CancellationToken cancel) {
            Debug.Assert(_cacheLock.CurrentCount == 0, "Cache must be locked before calling ReadCacheFromDiskAsync");

            var newCacheAge = DateTime.Now;
            var newCache = new Dictionary<string, PipPackageView>();
            using (await LockFile(_cachePath + ".lock", cancel).ConfigureAwait(false))
            using (var file = new StreamReader(_cachePath, Encoding.UTF8)) {
                try {
                    newCacheAge = File.GetLastWriteTime(_cachePath);
                } catch (IOException) {
                }

                string spec;
                while ((spec = await file.ReadLineAsync()) != null) {
                    cancel.ThrowIfCancellationRequested();
                    try {
                        var pv = new PipPackageView(this, spec, versionIsInstalled: false);
                        newCache[pv.Name] = pv;
                    } catch (FormatException) {
                    }
                }
            }

            _cache.Clear();
            foreach (var kv in newCache) {
                _cache[kv.Key] = kv.Value;
            }
            _cacheAge = newCacheAge;
        }

        #endregion


        private static string BasePackageCachePath {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Python Tools",
                    "PipCache",
#if DEBUG
                    "Debug",
#endif
                    AssemblyVersionInfo.VSVersion
                );
            }
        }

        private static string GetCachePath(Uri index, string indexName) {
            return Path.Combine(
                BasePackageCachePath,
                IndexNameSanitizerRegex.Replace(indexName, "_") + "_simple.cache"
            );
        }
    }
}
