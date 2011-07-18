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
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Hpc {
    static class Extensions {
        public static bool PublishBeforeRun(this IPythonProject project) {
            string optValue = project.GetProperty(ClusterOptions.PublishBeforeRunSetting);
            bool pubBeforeRun;
            if (optValue != null && Boolean.TryParse(optValue, out pubBeforeRun)) {
                return pubBeforeRun;
            } 
            return true;
        }

        public static Platform TargetPlatform(this IPythonProject project) {
            Platform platform;
            if (!Enum.TryParse<Platform>(project.GetProperty(ClusterOptions.TargetPlatformSetting), true, out platform)) {
                platform = Platform.X86;
            }
            return platform;
        }


        /// <summary>
        /// Reads a string from the socket which is encoded as a UTF8 string.
        /// </summary>
        internal static string ReadString(this Socket socket) {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer) == 4) {
                int filenameLen = BitConverter.ToInt32(cmd_buffer, 0);
                byte[] buffer = new byte[filenameLen];
                int bytesRead = 0;
                do {
                    bytesRead += socket.Receive(buffer, bytesRead, filenameLen - bytesRead, SocketFlags.None);
                } while (bytesRead != filenameLen);

                
                return Encoding.UTF8.GetString(buffer);
            } else {
                Debug.Assert(false, "Failed to read length");
            }
            return null;
        }

        internal static int ReadInt(this Socket socket) {
            byte[] cmd_buffer = new byte[4];
            if (socket.Receive(cmd_buffer) == 4) {
                return BitConverter.ToInt32(cmd_buffer, 0);
            }
            throw new InvalidOperationException();
        }

        internal static void SendString(this Socket socket, string text) {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            socket.Send(BitConverter.GetBytes(bytes.Length));
            socket.Send(bytes);
        }
    }
}
