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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PythonTools.MpiShim {
    class MpiShim {
        public static int Main(string[] args) {
            if (args.Length < 6) {
                Help();
                return -1;
            }

            int port;
            if (!Int32.TryParse(args[0], out port)) {
                Console.WriteLine("Got bad port number for arg 1");
                return -2;
            }

            Guid authGuid;
            if (!Guid.TryParse(args[1], out authGuid)) {
                Console.WriteLine("Got bad auth guid for arg 2");
                return -3;
            }

            var addrs = Dns.GetHostAddresses(args[2]);
            if (addrs.Length == 0) {
                Console.WriteLine("Cannot connect back to VisualStudio machine");
                return -4;
            }

            string curDir = args[3];
            string projectDir = args[4];
            string exe = args[5];
            if (!File.Exists(exe)) {
                Console.WriteLine("{0} does not exist, please install the Python interpreter or update the project debug settings to point at the correct interpreter.", exe);
            }

            Guid launchId = Guid.NewGuid();
            ManualResetEvent launchEvent = new ManualResetEvent(false);

#pragma warning disable 618 // Handle is obsolete but we need it.
            string msVsMonArgs = "/__dbgautolaunch 0x" + launchEvent.Handle.ToString("X") + " 0x" + Process.GetCurrentProcess().Id.ToString("X") + 
                                 " /name " + launchId.ToString() + 
                                 " /timeout:600";
#pragma warning restore 618

            Process msvsmonProc;
            try {
                var procStartInfo = new ProcessStartInfo(                
                    Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "msvsmon.exe"), 
                    msVsMonArgs);

                procStartInfo.UseShellExecute = false;
                msvsmonProc = Process.Start(procStartInfo);
            } catch(Exception e) {
                Console.WriteLine("Failed to start " + Path.Combine(Assembly.GetExecutingAssembly().Location, "msvsmon.exe"));
                Console.WriteLine(e);
                return -7;
            }

            var processEvent = new ManualResetEvent(true);
            processEvent.SafeWaitHandle = new SafeWaitHandle(msvsmonProc.Handle, false);
            
            if (WaitHandle.WaitAny(new[] { launchEvent, processEvent }) != 0) {
                Console.WriteLine("Failed to initialize msvsmon");
                return -5;
            }

            try {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP)) {
                    socket.Blocking = true;
                    socket.Connect(new IPEndPoint(addrs[0], port));

                    var secureStream = new NegotiateStream(new NetworkStream(socket, false), true);
                    secureStream.AuthenticateAsClient();

                    var writer = new StreamWriter(secureStream);

                    writer.WriteLine(authGuid.ToString());
                    writer.WriteLine(exe);
                    writer.WriteLine(curDir);
                    writer.WriteLine(projectDir);
                    writer.WriteLine(String.Join(" ", args, 6, args.Length - 6));
                    writer.WriteLine(launchId + "@" + Environment.MachineName);
                    writer.Flush();

                    var reader = new StreamReader(secureStream);
                    var procId = reader.ReadLine();

                    var processId = Int32.Parse(procId);
                    if (processId != 0) {
                        var debuggee = Process.GetProcessById(processId);
                        debuggee.WaitForExit();
                        msvsmonProc.WaitForExit();
                    } else {
                        int errorLen = Int32.Parse(reader.ReadLine());
                        char[] buffer = new char[errorLen];
                        int bytesRead = reader.Read(buffer, 0, buffer.Length);
                        Console.WriteLine("failed to get process to debug: {0}", new string(buffer, 0, bytesRead));
                        return -6;
                    }
                }
            } catch (SocketException) {
                Console.WriteLine("Failed to connect back to Visual Studio process.");
                msvsmonProc.Kill();
                return -8;
            }

            GC.KeepAlive(launchEvent);
            GC.KeepAlive(msvsmonProc);
            
            return 0;
        }

        private static void Help() {
            Console.WriteLine("{0}: <port number> <auth guid> <ip address> <command and args> <target command> <command line>", typeof(MpiShim).Assembly.GetName().Name);
        }
    }
}
