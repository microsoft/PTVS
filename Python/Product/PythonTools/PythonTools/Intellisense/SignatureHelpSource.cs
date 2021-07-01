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

namespace Microsoft.PythonTools.Intellisense
{
    internal class SignatureHelpSource : ISignatureHelpSource
    {
        private readonly ITextBuffer _textBuffer;
        private readonly SignatureHelpSourceProvider _provider;

        public SignatureHelpSource(SignatureHelpSourceProvider provider, ITextBuffer textBuffer)
        {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        public ISignature GetBestMatch(ISignatureHelpSession session)
        {
            return null;
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, System.Collections.Generic.IList<ISignature> signatures)
        {
            var span = session.GetApplicableSpan(_textBuffer);

            var sigs = _provider._serviceProvider.GetPythonToolsService().GetSignatures(
                session.TextView,
                _textBuffer.CurrentSnapshot,
                span
            );
            if (sigs != null)
            {
                ISignature curSig = sigs.Signatures
                     .OrderBy(s => s.Parameters.Count)
                     .FirstOrDefault(s => sigs.ParameterIndex < s.Parameters.Count);

                foreach (var sig in sigs.Signatures)
                {
                    signatures.Add(sig);
                }

                if (curSig != null)
                {
                    // save the current sig so we don't need to recalculate it (we can't set it until
                    // the signatures are added by our caller).
                    session.Properties?.AddProperty(typeof(PythonSignature), curSig);
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
