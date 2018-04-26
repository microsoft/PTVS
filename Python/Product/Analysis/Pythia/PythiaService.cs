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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Parsing.Ast;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Analysis.Pythia {

    internal sealed class PythiaService {
        public static readonly PythiaService Instance = new PythiaService();

        private event EventHandler<LogMessageEventArgs> OnLogMessage;

        public void LogMessage(MessageType type, string message) => OnLogMessage?.Invoke(this, new LogMessageEventArgs { type = type, message = message });

        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>>> _sequenceModel;

        private PythiaService() {
            _sequenceModel = LoadModel();
        }

        public List<CompletionItem> GetRecommendations(IEnumerable<CompletionItem> completionList,
                PythonAst ast, CompletionParams completionParams, int recommendataionLimit) {

            if (completionList != null && completionList.Count() > 0 && ast != null) {
                var currentPosition = ast.LocationToIndex(completionParams.position);

                var assignmentWalker = new AssignmentWalker();
                ast.Walk(assignmentWalker);

                var expressionWalker = new ExpressionWalker(assignmentWalker.Assignments, currentPosition);
                ast.Walk(expressionWalker);

                if (expressionWalker.EndIndexTypeNameMap.ContainsKey(currentPosition - 1)) {
                    var typeName = expressionWalker.EndIndexTypeNameMap[currentPosition - 1];

                    var inConditional = expressionWalker.IsCurrentPositionInConditional();
                    var previousMethods = expressionWalker.GetPreviousInvocations(typeName, 2);
                    Stack<string> sequences = new Stack<string>();
                    sequences.Push(Consts.NullSequence);
                    BuildSequences(previousMethods, sequences);
                    return ApplyModel(5, typeName, inConditional, completionList, sequences);

                }

            }

            return null;

        }

        private bool SupportType(string typeName)
                => _sequenceModel.ContainsKey(typeName);

        public List<CompletionItem> ApplyModel(
                int recommendataionLimit, string targetTypeName, bool inIfConditional,
                IEnumerable<CompletionItem> completionList, Stack<string> sequences) {
            Debug.WriteLine("Using Sequence Model to Recommend");

            if (!SupportType(targetTypeName)) {
                return null;
            }

            while (sequences.Count > 0) {
                var seq = sequences.Pop();

                if (!_sequenceModel[targetTypeName].ContainsKey(seq)) {
                    continue;
                }

                var recommendations = GetModelRecommendations(
                    _sequenceModel[targetTypeName][seq], recommendataionLimit, inIfConditional, targetTypeName, completionList);

                if (recommendations.Count == 0) {
                    continue;
                }


                return recommendations;
            }
            return null;
        }

        private static List<CompletionItem> GetModelRecommendations(
                IReadOnlyList<IReadOnlyList<string>> models,
                int recommendataionLimit,
                bool inIfConditional,
                string typeName,
                IEnumerable<CompletionItem> completionList) {
            var count = 0;

            var model = models[(inIfConditional && models[1].Count > 0) ? 1 : 0];
            var items = new List<CompletionItem>();
            foreach (var recommendation in model) {
                var item = GetRecommendation(completionList, recommendation, count);
                if (item.HasValue) {
                    items.Add(item.Value);
                    count++;

                    if (count >= recommendataionLimit) {
                        break;
                    }
                }
            }

            return items;
        }

        private static CompletionItem? GetRecommendation(IEnumerable<CompletionItem> completionList, string recommendation, int rank) {

            foreach (var item in completionList) {
                if (item.label == recommendation) {
                    return BuildPythiaCompletionItem(item, rank);
                }
            }
            return ToCompletionItem(recommendation, rank);
        }

        private static CompletionItem ToCompletionItem(string itemLabel, int rank) {
            var kind = CompletionItemKind.Method;
            if (itemLabel[0] >= 'A' && itemLabel[0] <= 'Z') {
                kind = CompletionItemKind.Constant;
            }
            var res = new CompletionItem {
                label = Consts.UnicodeStar + itemLabel,
                insertText = itemLabel,
                documentation = "",
                // Place Pythia Items on the top
                sortText = "0." + rank,
                kind = kind,
                _kind = kind.ToString().ToLowerInvariant()
            };

            return res;
        }

        private static CompletionItem BuildPythiaCompletionItem(CompletionItem item, int rank) {
            var res = new CompletionItem {
                label = Consts.UnicodeStar + item.label,
                insertText = item.insertText,
                documentation = item.documentation,
                // Place Pythia Items on the top
                sortText = "0." + rank,
                kind = item.kind,
                _kind = item._kind
            };

            return res;
        }

        private static void BuildSequences(List<string> prevMethods, Stack<string> sequences) {

            int count = prevMethods.Count;
            if (count == 0) {
                return;
            }

            Debug.WriteLine("Using Sequence Model to Recommend");

            // add the first
            var seq = Consts.NullSequence + Consts.SequenceDelimiter + prevMethods[0];
            sequences.Push(seq);

            if (count >= 2) {
                seq = prevMethods[count - 1] + Consts.SequenceDelimiter + prevMethods[count - 2];
                sequences.Push(seq);
            }
        }

        private Node FindPreviousNode(PythonAst ast, int position) {
            var finder = new ExpressionFinder(ast, GetExpressionOptions.Complete);

            Node previousNode = finder.GetExpression(position);

            if (previousNode == null && position > 0) {
                position--;
                previousNode = finder.GetExpression(position);
            }

            return previousNode;
        }


        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>>> LoadModel() {
            var watch = new Stopwatch();

            try {
                watch.Start();

                var serializer = new JsonSerializer();

                using (var modelStream = PythiaUtil.ReadModel(Consts.SequenceModelPath))
                using (var jsonReader = new JsonTextReader(new StreamReader(modelStream))) {
                    var sequenceModel = serializer.Deserialize<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>>>>(jsonReader);

                    //PythiaTelemetry.SendLoadModel(watch.ElapsedMilliseconds);


                    return sequenceModel;
                }
            } catch (Exception ex) {
                //PythiaTelemetry.SendLoadModelFailure(ex);
                LogMessage(MessageType.Error, $"Pythia Model loading failed: {ex.ToString()}");
            } finally {
                long elapsedMS = watch.ElapsedMilliseconds;
                watch.Stop();

                LogMessage(MessageType.Info, $"Pythia model loading took: {elapsedMS} milliseconds");
            }

            return null;
        }
    }
}
