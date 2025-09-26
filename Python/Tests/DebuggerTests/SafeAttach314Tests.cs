using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools.Debugger.Concord;

namespace DebuggerTests {
    [TestClass]
    public class SafeAttach314Tests {
        private string _tempDir;
        private string _loaderPath;

        [TestInitialize]
        public void Setup() {
            _tempDir = Path.Combine(Path.GetTempPath(), "PTVS_SafeAttachTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _loaderPath = Path.Combine(_tempDir, "ptvsd_loader.py");
            File.WriteAllText(_loaderPath, "# test loader\n");
        }

        [TestCleanup]
        public void Cleanup() {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
        }

        [TestMethod]
        public void PrepareLoaderBuffer_Fits_NoTruncation() {
            // Arrange
            ulong size = 4096; // ample space
            // Act
            bool ok = RemoteAttach314.PrepareLoaderBuffer(_loaderPath, size, out var buf, out bool truncated);
            // Assert
            Assert.IsTrue(ok, "Expected success for valid size");
            Assert.IsFalse(truncated, "Path should not be truncated");
            var actual = System.Text.Encoding.UTF8.GetString(buf).TrimEnd('\0');
            Assert.AreEqual(Path.GetFullPath(_loaderPath), actual, "Buffer content mismatch");
            Assert.AreEqual('\0', (char)buf[buf.Length - 1], "Buffer must be NUL terminated");
        }

        [TestMethod]
        public void PrepareLoaderBuffer_TooSmall_Fails() {
            // scriptPathSize = 1 cannot hold even NUL + 1 char
            bool ok = RemoteAttach314.PrepareLoaderBuffer(_loaderPath, 1, out var _, out bool _);
            Assert.IsFalse(ok, "Should fail when buffer too small");
        }

        [TestMethod]
        public void PrepareLoaderBuffer_ZeroSize_Fails() {
            bool ok = RemoteAttach314.PrepareLoaderBuffer(_loaderPath, 0, out var _, out bool _);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void PrepareLoaderBuffer_LargeRejected_Fails() {
            // Over 1,000,000 guard should fail
            bool ok = RemoteAttach314.PrepareLoaderBuffer(_loaderPath, 1_000_001, out var _, out bool _);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void PrepareLoaderBuffer_Truncates_WhenBufferSmallerThanPath() {
            // Force truncation by choosing a size smaller than the full path length (but >=2 for payload+NUL)
            var full = Path.GetFullPath(_loaderPath);
            ulong size = (ulong)Math.Max(2, full.Length / 2); // ensure at least 2
            bool ok = RemoteAttach314.PrepareLoaderBuffer(_loaderPath, size, out var buf, out bool truncated);
            Assert.IsTrue(ok, "Expected success with truncation");
            Assert.IsTrue(truncated, "Expected truncation flag");
            Assert.AreEqual((int)size, buf.Length, "Buffer length should equal requested size (payload+NUL)");
            Assert.AreEqual(0, buf[buf.Length - 1], "Last byte must be NUL");
        }
    }
}
