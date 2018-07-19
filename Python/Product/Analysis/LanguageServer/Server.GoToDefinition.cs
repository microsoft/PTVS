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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        public override Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params) => GotoDefinition(@params, CancellationToken.None);

        internal async Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var references = await FindReferences(new ReferencesParams {
                textDocument = @params.textDocument,
                position = @params.position,
                context = new ReferenceContext {
                    includeDeclaration = true,
                    _includeValues = true
                }
            }, cancellationToken);
            return references.Where(r => r._kind == ReferenceKind.Definition && r.uri != null).ToArray();
        }
    }
}
