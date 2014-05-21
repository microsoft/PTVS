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
using System.IO;
using System.Net.Security;
using System.Security.Authentication;

namespace Microsoft.PythonTools.Debugger.Transports {
    internal class TcpsTransport : TcpTransport {
        public override Stream Connect(Uri uri, bool requireAuthentication) {
            var rawStream = base.Connect(uri, requireAuthentication);
            try {
                var sslStream = new SslStream(rawStream, false, (sender, cert, chain, errs) => {
                    if (errs == SslPolicyErrors.None || !requireAuthentication) {
                        return true;
                    }

                    string errText = string.Format("Could not establish secure connection to {0} because of the following SSL issues:\n\n", uri);
                    if ((errs & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) {
                        errText += "- no remote certificate provided\n";
                    }
                    if ((errs & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) {
                        errText += "- remote certificate name does not match hostname\n";
                    }
                    if ((errs & SslPolicyErrors.RemoteCertificateChainErrors) != 0) {
                        errText += "- remote certificate is not trusted\n";
                    }

                    throw new AuthenticationException(errText);
                });

                sslStream.AuthenticateAsClient(uri.Host);
                rawStream = null;
                return sslStream;
            } catch (IOException ex) {
                throw new ConnectionException(ConnErrorMessages.RemoteNetworkError, ex);
            } finally {
                if (rawStream != null) {
                    rawStream.Dispose();
                }
            }
        }
    }
}
