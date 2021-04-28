using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Repl.Completion {
    internal static class Utilities {
        internal static void ApplyTextEdit(TextEdit textEdit, ITextSnapshot snapshot, ITextBuffer textBuffer) {
            Utilities.ApplyTextEdits(new[] { textEdit }, snapshot, textBuffer);
        }

        internal static void ApplyTextEdits(IEnumerable<TextEdit> textEdits, ITextSnapshot snapshot, ITextBuffer textBuffer) {
            using (var vsTextEdit = textBuffer.CreateEdit()) {
                foreach (var textEdit in textEdits) {
                    if (textEdit.Range.Start == textEdit.Range.End) {
                        var position = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                        if (position > -1) {
                            var span = GetTranslatedSpan(position, 0, snapshot, vsTextEdit.Snapshot);
                            vsTextEdit.Insert(span.Start, textEdit.NewText);
                        }
                    } else if (string.IsNullOrEmpty(textEdit.NewText)) {
                        var startPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                        var endPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.End);
                        var difference = endPosition - startPosition;
                        if (startPosition > -1 && endPosition > -1 && difference > 0) {
                            var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                            vsTextEdit.Delete(span);
                        }
                    } else {
                        var startPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.Start);
                        var endPosition = snapshot.GetSnapshotPositionFromProtocolPosition(textEdit.Range.End);
                        var difference = endPosition - startPosition;

                        if (startPosition > -1 && endPosition > -1 && difference > 0) {
                            var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                            vsTextEdit.Replace(span, textEdit.NewText);
                        }
                    }
                }

                vsTextEdit.Apply();
            }
        }   

        internal static CompletionContext GetContextFromTrigger(VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionTrigger trigger, bool hasRegisteredTriggerCharacter) {
            var triggerString = trigger.Character == default(char) ? null : trigger.Character.ToString(CultureInfo.InvariantCulture);
            return new CompletionContext {
                TriggerCharacter = hasRegisteredTriggerCharacter ? triggerString : null,
                TriggerKind = hasRegisteredTriggerCharacter ? CompletionTriggerKind.TriggerCharacter : CompletionTriggerKind.Invoked,
            };
        }

        internal static string GetFilterName(CompletionItemKind? completionKind) {
            return completionKind switch {
                null => string.Empty,
                CompletionItemKind.Class => LanguageServerResources.CompletionFilterClass,
                CompletionItemKind.Color => LanguageServerResources.CompletionFilterColor,
                CompletionItemKind.Constant => LanguageServerResources.CompletionFilterConstant,
                CompletionItemKind.Constructor => LanguageServerResources.CompletionFilterConstructor,
                CompletionItemKind.Enum => LanguageServerResources.CompletionFilterEnum,
                CompletionItemKind.EnumMember => LanguageServerResources.CompletionFilterEnumMember,
                CompletionItemKind.Event => LanguageServerResources.CompletionFilterEvent,
                CompletionItemKind.Field => LanguageServerResources.CompletionFilterField,
                CompletionItemKind.File => LanguageServerResources.CompletionFilterFile,
                CompletionItemKind.Folder => LanguageServerResources.CompletionFilterFolder,
                CompletionItemKind.Function => LanguageServerResources.CompletionFilterFunction,
                CompletionItemKind.Interface => LanguageServerResources.CompletionFilterInterface,
                CompletionItemKind.Keyword => LanguageServerResources.CompletionFilterKeyword,
                CompletionItemKind.Method => LanguageServerResources.CompletionFilterMethod,
                CompletionItemKind.Module => LanguageServerResources.CompletionFilterModule,
                CompletionItemKind.Operator => LanguageServerResources.CompletionFilterOperator,
                CompletionItemKind.Property => LanguageServerResources.CompletionFilterProperty,
                CompletionItemKind.Reference => LanguageServerResources.CompletionFilterReference,
                CompletionItemKind.Snippet => LanguageServerResources.CompletionFilterSnippet,
                CompletionItemKind.Struct => LanguageServerResources.CompletionFilterStruct,
                CompletionItemKind.Text => LanguageServerResources.CompletionFilterText,
                CompletionItemKind.TypeParameter => LanguageServerResources.CompletionFilterTypeParameter,
                CompletionItemKind.Unit => LanguageServerResources.CompletionFilterUnit,
                CompletionItemKind.Value => LanguageServerResources.CompletionFilterValue,
                CompletionItemKind.Variable => LanguageServerResources.CompletionFilterVariable,
                _ => Enum.GetName(typeof(CompletionItemKind), completionKind)
            };
        }
    }
}
