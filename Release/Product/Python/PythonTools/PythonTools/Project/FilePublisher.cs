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
using System.ComponentModel.Composition;
using System.IO;
using System.Net;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Publishes files to a file share
    /// </summary>
    [Export(typeof(IProjectPublisher))]
    class FilePublisher : IProjectPublisher {
        #region IProjectPublisher Members

        public void PublishFiles(IPublishProject project, Uri destination) {
            ImpersonationHelper helper = null;
            NetworkCredential creds = null;
            bool impersonated = false;

            // Use ECWGC here so that a filter cannot run while we're impersonated (and so we always un-impersonate)
            System.Runtime.CompilerServices.RuntimeHelpers.ExecuteCodeWithGuaranteedCleanup(
                (_) => {
                    var files = project.Files;

                    for (int i = 0; i < files.Count; i++) {
                        var item = files[i];

                        try {
                            // try copying without impersonating first...
                            CopyOneFile(destination, item, ref helper, creds);
                        } catch (UnauthorizedAccessException) {
                            if (impersonated) {
                                // user entered incorrect credentials
                                throw;
                            }

                            // prompt for new credentials and switch to them if available.
                            var res = VsCredentials.PromptForCredentials(PythonToolsPackage.Instance, destination, new[] { "NTLM" }, "", out creds);
                            if (res == System.Windows.Forms.DialogResult.OK) {
                                impersonated = true;
                                helper = new ImpersonationHelper(creds);
                            } else {
                                throw;
                            }

                            // re-try the file copy w/ the new credentials.
                            CopyOneFile(destination, item, ref helper, creds);
                        }

                        project.Progress = (int)(((double)i / (double)files.Count) * 100);
                    }
                },
                (data, exception) => {
                    if (helper != null) {
                        helper.Dispose();
                    }
                },
                null
            );           
        }

        private static void CopyOneFile(Uri destination, IPublishFile item, ref ImpersonationHelper impersonate, NetworkCredential creds) {
            var destFile = CommonUtils.GetAbsoluteFilePath(destination.LocalPath, item.DestinationFile);
            string destDir = Path.GetDirectoryName(destFile);
            if (!Directory.Exists(destDir)) {
                // don't create a file share (\\foo\bar)
                if (!Path.IsPathRooted(destDir) || Path.GetPathRoot(destDir) != destDir) {
                    Directory.CreateDirectory(destDir);
                }
            }

            if (impersonate != null) {
                // we need to unimpersonate to read the file, then re-impersonate when we're done.
                using (FileStream destStream = new FileStream(destFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
                    impersonate.UndoImpersonate();
                    impersonate = null;
                    try {

                        using (FileStream file = new FileStream(item.SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) {
                            byte[] buffer = new byte[1024];
                            int bytesRead;
                            while ((bytesRead = file.Read(buffer, 0, buffer.Length)) != 0) {
                                destStream.Write(buffer, 0, bytesRead);
                            }
                        }
                    } finally {
                        impersonate = new ImpersonationHelper(creds);
                    }
                }
            } else {
                File.Copy(item.SourceFile, destFile, true);
            }
        }

        public string DestinationDescription {
            get { return "file path";  }
        }

        public string Schema {
            get { return "file"; }
        }

        #endregion
    }
}
