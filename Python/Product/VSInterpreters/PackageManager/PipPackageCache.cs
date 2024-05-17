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
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter {
    class PipPackageCache : IDisposable {
        private static readonly Dictionary<string, PipPackageCache> _knownCaches =
            new Dictionary<string, PipPackageCache>();
        private static readonly object _knownCachesLock = new object();

        private readonly Uri _index;
        private readonly string _indexName;
        private readonly string _cachePath;

        protected readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1);
        protected readonly Dictionary<string, PackageSpec> _cache;
        protected DateTime _cacheAge;

        // The live cache contains names of package specs that are fully up to date
        // in the cache.
        protected readonly HashSet<string> _liveCache;
        protected DateTime _liveCacheAge;

        protected int _userCount; // protected by _knownCachesLock
        private long _writeVersion;

        protected bool _isDisposed;

        private readonly static Regex IndexNameSanitizerRegex = new Regex(@"\W");
        private static readonly Regex SimpleListRegex = new Regex(@"\<a.*?\>(?<package>.+?)\<",
            RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1.0)
        );

        // Don't make multiple requests for the same package
        private readonly Dictionary<string, TaskCompletionSource<PackageSpec>> _activeRequests = new Dictionary<string, TaskCompletionSource<PackageSpec>>();

        // These constants are substituted where necessary, but are not stored
        // in instance variables so we can differentiate between set and unset.
        private const string DefaultIndexFwLink = "https://go.microsoft.com/fwlink/?linkid=834538";
        private static Task<Uri> _DefaultIndexTask = Task.Run(() => Resolve(DefaultIndexFwLink));
        private const string DefaultIndexName = "PyPI";

        private const string UserAgent = "PythonToolsForVisualStudio/" + AssemblyVersionInfo.Version;

        private static readonly TimeSpan CacheAgeLimit = TimeSpan.FromHours(6);
        private static readonly TimeSpan LiveCacheAgeLimit = TimeSpan.FromHours(1);

        protected PipPackageCache(
            Uri index,
            string indexName,
            string cachePath
        ) {
            _index = index;
            _indexName = indexName;
            _cachePath = cachePath;
            _cache = new Dictionary<string, PackageSpec>();
            _liveCacheAge = DateTime.UtcNow;
            _liveCache = new HashSet<string>();
        }

        public static PipPackageCache GetCache(Uri index = null, string indexName = null) {
            PipPackageCache cache;
            var key = index?.AbsoluteUri ?? string.Empty;
            lock (_knownCachesLock) {
                if (!_knownCaches.TryGetValue(key, out cache)) {
                    _knownCaches[key] = cache = new PipPackageCache(
                        index,
                        indexName,
                        GetCachePath(indexName ?? DefaultIndexName)
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
            GC.SuppressFinalize(this);
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
                        _knownCaches.Remove(_index?.AbsoluteUri ?? string.Empty);
                    }
                }
            }
        }

        public Uri Index {
            get { return _index ?? _DefaultIndexTask.Result; }
        }

        public string IndexName {
            get { return _indexName ?? DefaultIndexName; }
        }

        public bool HasExplicitIndex {
            get { return _index != null; }
        }


        public async Task<IList<PackageSpec>> GetAllPackagesAsync(CancellationToken cancel) {
            await _cacheLock.WaitAsync(cancel).ConfigureAwait(false);
            try {
                if (!_cache.Any()) {
                    try {
                        await ReadCacheFromDiskAsync(cancel).ConfigureAwait(false);
                    } catch (IOException) {
                        _cacheAge = DateTime.MinValue;
                    }
                }
                if (_cacheAge + CacheAgeLimit < DateTime.UtcNow) {
                    await RefreshCacheAsync(cancel).ConfigureAwait(false);
                    lock (_liveCache) {
                        _liveCache.Clear();
                        _liveCacheAge = DateTime.UtcNow;
                    }
                }

                return _cache.Values.ToList();
            } finally {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Use some rough heuristicts to try and only show the first section of
        /// a package description.
        /// 
        /// Most descriptions start with a summary line or title, followed by a
        /// blank line or "====..." style separator.
        /// </summary>
        private static bool IsSeparatorLine(string s) {
            if (string.IsNullOrWhiteSpace(s)) {
                return true;
            }

            if (s.Length > 2) {
                var first = s.FirstOrDefault(c => !char.IsWhiteSpace(c));
                if (first != default(char)) {
                    if (s.All(c => char.IsWhiteSpace(c) || c == first)) {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsInLiveCache(string name) {
            lock (_liveCache) {
                if (_liveCacheAge + LiveCacheAgeLimit < DateTime.UtcNow) {
                    _liveCache.Clear();
                    _liveCacheAge = DateTime.UtcNow;
                    return false;
                } else {
                    return _liveCache.Contains(name);
                }
            }
        }

        private void AddToLiveCache(string name) {
            lock (_liveCache) {
                if (_liveCacheAge + LiveCacheAgeLimit < DateTime.UtcNow) {
                    _liveCache.Clear();
                    _liveCacheAge = DateTime.UtcNow;
                }
                _liveCache.Add(name);
            }
        }

        public async Task<PackageSpec> GetPackageInfoAsync(PackageSpec entry, CancellationToken cancel) {
            string description = null;
            List<string> versions = null;

            bool useCache = IsInLiveCache(entry.Name);

            if (useCache) {
                using (await _cacheLock.LockAsync(cancel)) {
                    PackageSpec result;
                    if (_cache.TryGetValue(entry.Name, out result)) {
                        return result.Clone();
                    } else {
                        return new PackageSpec();
                    }
                }
            }

            TaskCompletionSource<PackageSpec> tcs = null;
            Task<PackageSpec> t = null;
            lock (_activeRequests) {
                if (_activeRequests.TryGetValue(entry.Name, out tcs)) {
                    t = tcs.Task;
                } else {
                    _activeRequests[entry.Name] = tcs = new TaskCompletionSource<PackageSpec>();
                }
            }

            if (t != null) {
                return (await t).Clone();
            }

            try {
                ServicePointManager.CheckCertificateRevocationList = true;
                using (var client = new WebClient()) {
                    Stream data;
                    client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                    try {
                        data = await client.OpenReadTaskAsync(new Uri(Index, entry.Name + "/json"))
                            .ConfigureAwait(false);
                    } catch (WebException) {
                        // No net access or no such package
                        AddToLiveCache(entry.Name);
                        return new PackageSpec();
                    }

                    try {
                        using (var reader = JsonReaderWriterFactory.CreateJsonReader(data, new XmlDictionaryReaderQuotas())) {
                            var doc = XDocument.Load(reader);

                            // TODO: Get package URL
                            //url = (string)doc.Document
                            //    .Elements("root")
                            //    .Elements("info")
                            //    .Elements("package_url")
                            //    .FirstOrDefault();

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

                bool changed = false;
                PackageSpec result;

                using (await _cacheLock.LockAsync(cancel)) {
                    if (!_cache.TryGetValue(entry.Name, out result)) {
                        result = _cache[entry.Name] = new PackageSpec(entry.Name);
                    }

                    if (!string.IsNullOrEmpty(description)) {
                        var lines = description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        var firstLine = string.Join(
                            " ",
                            lines.TakeWhile(s => !IsSeparatorLine(s)).Select(s => s.Trim())
                        );
                        if (firstLine.Length > 500) {
                            firstLine = firstLine.Substring(0, 497) + "...";
                        }
                        if (firstLine == "UNKNOWN") {
                            firstLine = string.Empty;
                        }

                        result.Description = firstLine;
                        changed = true;
                    }

                    if (versions != null) {
                        var updateVersion = PackageVersion.TryParseAll(versions)
                            .Where(v => v.IsFinalRelease)
                            .OrderByDescending(v => v)
                            .FirstOrDefault();
                        result.ExactVersion = updateVersion;
                        changed = true;
                    }
                }

                if (changed) {
                    TriggerWriteCacheToDisk();
                    AddToLiveCache(entry.Name);
                }

                var r = result.Clone();

                // Inform other waiters that we have completed
                tcs.TrySetResult(r);

                return r;
            } catch (Exception ex) {
                tcs.TrySetException(ex);
                throw;
            } finally {
                lock (_activeRequests) {
                    _activeRequests.Remove(entry.Name);
                }
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
                        var dir = PathUtils.GetParent(filename);
                        if (!Directory.Exists(dir)) {
                            Directory.CreateDirectory(dir);
                        } else {
                            throw;
                        }
                    } catch (IOException) {
                    }
                }
                await Task.Delay(100);
            }

            return stream;
        }

        private async Task RefreshCacheAsync(CancellationToken cancel) {
            Debug.Assert(_cacheLock.CurrentCount == 0, "Cache must be locked before calling RefreshCacheAsync");

            string htmlList;
            using (var client = new WebClient()) {
                client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                // ../simple is a list of <a href="package">package</a>
                try {
                    htmlList = await client.DownloadStringTaskAsync(new Uri(Index, "../simple")).ConfigureAwait(false);
                } catch (WebException) {
                    // No net access, so can't refresh
                    return;
                }
            }

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
                        _cache[package] = new PackageSpec(package);
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

            _cacheAge = DateTime.UtcNow;
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
                        await file.WriteLineAsync("{0}{1}{2}{3}".FormatUI(
                            keyValue.Value.Name,
                            keyValue.Value.Constraint,
                            string.IsNullOrEmpty(keyValue.Value.Description) ? "" : " #",
                            Uri.EscapeDataString(keyValue.Value.Description ?? "")
                        ));
                    }
                }
            } catch (IOException ex) {
                Debug.Fail("Failed to write cache file: " + ex.ToString());
                // Otherwise, just keep the cache in memory.
            }
        }

        protected async Task ReadCacheFromDiskAsync(CancellationToken cancel) {
            Debug.Assert(_cacheLock.CurrentCount == 0, "Cache must be locked before calling ReadCacheFromDiskAsync");

            var newCacheAge = DateTime.UtcNow;
            var newCache = new Dictionary<string, PackageSpec>();
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
                        int descriptionStart = spec.IndexOfOrdinal(" #");
                        if (descriptionStart > 0) {
                            var pv = PackageSpec.FromRequirement(spec.Remove(descriptionStart));
                            pv.Description = Uri.UnescapeDataString(spec.Substring(descriptionStart + 2));
                            newCache[pv.Name] = pv;
                        } else {
                            var pv = PackageSpec.FromRequirement(spec);
                            newCache[pv.Name] = pv;
                        }
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
                    AssemblyVersionInfo.Version
                );
            }
        }

        private static Uri Resolve(string uri) {
            var req = WebRequest.CreateHttp(uri);
            req.Method = "HEAD";
            req.AllowAutoRedirect = false;
            req.UserAgent = UserAgent;
            try {
                using (var resp = req.GetResponse()) {
                    return new Uri(resp.Headers.Get("Location") ?? uri);
                }
            } catch (WebException) {
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Debug.Fail(ex.ToString());
                // Nowhere else to report this error, so just swallow it
            }
            return new Uri(uri);
        }

        private static string GetCachePath(string indexName) {
            return Path.Combine(
                BasePackageCachePath,
                IndexNameSanitizerRegex.Replace(indexName, "_") + "_simple.cache"
            );
        }
    }
}
