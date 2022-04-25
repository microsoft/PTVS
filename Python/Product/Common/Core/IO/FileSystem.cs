// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Common.Core.OS;

namespace Microsoft.PythonTools.Common.Core.IO {
    public sealed class FileSystem : IFileSystem {
        private readonly IOSPlatform _os;

        public FileSystem(IOSPlatform os) {
            _os = os;
        }

        public IFileSystemWatcher CreateFileSystemWatcher(string path, string filter) => new FileSystemWatcherProxy(path, filter);
        public IDirectoryInfo GetDirectoryInfo(string directoryPath) => new DirectoryInfoProxy(directoryPath);
        public bool FileExists(string path) => File.Exists(path);

        public long FileSize(string path) {
            var fileInfo = new FileInfo(path);
            return fileInfo.Length;
        }

        public string ReadAllText(string filePath) {
            if (PathUtils.TryGetZipFilePath(filePath, out var zipPath, out var relativeZipPath)) {
                return PathUtils.GetZipContent(zipPath, relativeZipPath);
            }
            return File.ReadAllText(filePath);
        }

        public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
        public IEnumerable<string> FileReadAllLines(string path) => File.ReadLines(path);
        public void FileWriteAllLines(string path, IEnumerable<string> contents) => File.WriteAllLines(path, contents);
        public byte[] FileReadAllBytes(string path) => File.ReadAllBytes(path);
        public void FileWriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);
        public Stream CreateFile(string path) => File.Create(path);
        public Stream FileOpen(string path, FileMode mode) => File.Open(path, mode);
        public Stream FileOpen(string path, FileMode mode, FileAccess access, FileShare share) => File.Open(path, mode, access, share);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public FileAttributes GetFileAttributes(string path) => File.GetAttributes(path);
        public void SetFileAttributes(string fullPath, FileAttributes attributes) => File.SetAttributes(fullPath, attributes);
        public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);

        public Version GetFileVersion(string path) {
            var fvi = FileVersionInfo.GetVersionInfo(path);
            return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
        }

        public void DeleteFile(string path) => File.Delete(path);
        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
        public string[] GetFileSystemEntries(string path, string searchPattern, SearchOption options) => Directory.GetFileSystemEntries(path, searchPattern, options);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public string[] GetFiles(string path) => Directory.GetFiles(path);
        public string[] GetFiles(string path, string pattern) => Directory.GetFiles(path, pattern);
        public string[] GetFiles(string path, string pattern, SearchOption option) => Directory.GetFiles(path, pattern, option);
        public string[] GetDirectories(string path) => Directory.GetDirectories(path);

        public bool IsPathUnderRoot(string root, string path) => Path.GetFullPath(path).StartsWith(root, StringComparison);
        public StringComparison StringComparison => _os.IsLinux ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }
}
