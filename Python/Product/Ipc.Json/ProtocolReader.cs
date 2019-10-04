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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Ipc.Json {
    class ProtocolReader {
        private Stream _stream;
        private List<byte> _buffer = new List<byte>();

        public ProtocolReader(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }
            _stream = stream;
        }

        /// <summary>
        /// Reads an ASCII encoded header line asynchronously from the current stream
        /// and returns the data as a string. Line termination chars are '\r\n' and
        /// are excluded from the return value. Keeps reading until it finds it, and
        /// if it reaches the end of the stream (no more data is read) without finding
        /// it then it returns <c>null</c>.
        /// </summary>
        public async Task<string> ReadHeaderLineAsync() {
            // Keep reading into the buffer until it contains the '\r\n'.
            int searchStartPos = 0;
            int newLineIndex;
            while ((newLineIndex = IndexOfNewLineInBuffer(searchStartPos)) < 0) {
                searchStartPos = Math.Max(0, _buffer.Count - 1);
                if (await ReadIntoBuffer() == 0) {
                    return null;
                }
            }

            var line = _buffer.Take(newLineIndex).ToArray();
            _buffer.RemoveRange(0, newLineIndex + 2);
            return Encoding.ASCII.GetString(line);
        }

        /// <summary>
        /// Reads all bytes from the current position to the end of the
        /// stream asynchronously.
        /// </summary>
        public async Task<byte[]> ReadToEndAsync() {
            int read;
            while ((read = await ReadIntoBuffer()) > 0) {
            }

            var all = _buffer.ToArray();
            _buffer.Clear();
            return all;
        }

        /// <summary>
        /// Reads a number of bytes asynchronously from the current stream.
        /// </summary>
        /// <param name="byteCount">Number of bytes to read.</param>
        /// <remarks>
        /// May return fewer bytes than requested.
        /// </remarks>
        public async Task<byte[]> ReadContentAsync(int byteCount) {
            if (byteCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "Value cannot be negative.");
            }

            while (_buffer.Count < byteCount) {
                if (await ReadIntoBuffer() == 0) {
                    break;
                }
            }

            var actualCount = Math.Min(byteCount, _buffer.Count);
            var result = _buffer.Take(actualCount).ToArray();
            _buffer.RemoveRange(0, actualCount);
            return result;
        }

        private int IndexOfNewLineInBuffer(int searchStartPos) {
            for (int i = searchStartPos; i < _buffer.Count - 1; i++) {
                if (_buffer[i] == 13 && _buffer[i + 1] == 10) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Reads bytes from the stream into the buffer, in chunks.
        /// </summary>
        /// <returns>Number of bytes that were added to the buffer.</returns>
        private async Task<int> ReadIntoBuffer() {
            var temp = new byte[1024];
            var read = await _stream.ReadAsync(temp, 0, temp.Length);
            _buffer.AddRange(temp.Take(read).ToArray());
            return read;
        }
    }
}
