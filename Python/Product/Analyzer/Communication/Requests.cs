using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Cdp;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.Communication {
    public static class Requests {
        public static readonly Dictionary<string, Type> RegisteredTypes = CollectCommands();

        private static Dictionary<string, Type> CollectCommands() {
            Dictionary<string, Type> all = new Dictionary<string, Type>();
            foreach (var type in typeof(Requests).Assembly.GetExportedTypes()) {
                if (type.IsSubclassOf(typeof(Request))) {
                    var command = type.GetField("Command");
                    if (command != null) {
                        all["request." + (string)command.GetRawConstantValue()] = type;
                    }
                } else if (type.IsSubclassOf(typeof(Event))) {
                    var name = type.GetField("Name");
                    if (name != null) {
                        all["event." + (string)name.GetRawConstantValue()] = type;
                    }
                }
            }
            return all;
        }
    }

    public class HasErrorsRequest : Request<HasErrorsResponse> {
        public const string Command = "hasErrors";

        public int fileId;

        public HasErrorsRequest() : base(Command) {
        }
    }

    public class HasErrorsResponse : Response {
        public bool hasErrors;
    }

    public class AddFileRequest : Request<AddFileResponse> {
        public const string Command = "addFile";

        public string path;
        public string addingFromDir;

        public AddFileRequest() : base(Command) {
        }
    }

    public class AddFileResponse : Response {
        public int fileId;
    }

    public sealed class AddDirectoryRequest : Request<Response> {
        public const string Command = "addDir";

        public string dir;
        public AddDirectoryRequest() : base(Command) {
        }
    }

    public sealed class RemoveDirectoryRequest : Request<Response> {
        public const string Command = "removeDir";

        public string dir;

        public RemoveDirectoryRequest() : base(Command) {
        }
    }

    public sealed class UnloadFileRequest : Request<Response> {
        public const string Command = "unloadFile";

        public int fileId;
        public UnloadFileRequest() : base(Command) {
        }
    }


    public sealed class DirectoryFileAddedEvent : Event {
        public const string Name = "directoryFileAdded";

        public string filename;
        public int fileId;

        public DirectoryFileAddedEvent() : base(Name) {
        }
    }

    public sealed class FileContentEvent : Event {
        public const string Name = "fileContent";

        public FileContentEvent() : base(Name) {
        }

        public int fileId;
        public string content;
    }

    public sealed class FileChangedEvent : Event {
        public const string Name = "fileChanged";

        public FileChangedEvent() : base(Name) {
        }

        public int fileId;
        public ChangeInfo[] changes;
    }

    public sealed class ChangeInfo {
        public string newText;
        public int start;
        public int length;
    }

    public class EnqueueFileResponse : Response {
        public int fileId;
    }

    public sealed class AddZipArchiveRequest : Request<Response> {
        public const string Command = "addZipArchive";
        public string archive;

        public AddZipArchiveRequest() : base(Command) {
        }
    }

    public class TopLevelCompletionsRequest : Request<CompletionsResponse> {
        public const string Command = "topCompletions";

        public int fileId;
        public int location, column;
        public GetMemberOptions options;

        public TopLevelCompletionsRequest() : base(Command) {
        }
    }

    public class GetModulesRequest : Request<CompletionsResponse> {
        public const string Command = "getModules";

        public int fileId;
        public bool topLevelOnly;

        public GetModulesRequest() : base(Command) {
        }
    }

    public class GetModuleMembers : Request<CompletionsResponse> {
        public const string Command = "getModuleMembers";

        public int fileId;
        public string[] package;
        public bool includeMembers;

        public GetModuleMembers() : base(Command) {
        }
    }

    public class CompletionsRequest : Request<CompletionsResponse> {
        public const string Command = "completions";

        public int fileId;
        public string text;
        public int location, column;
        public GetMemberOptions options;
        public bool forceCompletions;

        public CompletionsRequest() : base(Command) {


        }
    }

    public class SignaturesRequest : Request<SignaturesResponse> {
        public const string Command = "sigs";

        public SignaturesRequest() : base(Command) {
        }

        public string text;
        public int location, column;
        public int fileId;
    }

    public sealed class ModulesChangedEvent : Event {
        public const string Name = "modulesChanged";

        public ModulesChangedEvent() : base(Name)  {
        }
    }

    public sealed class AnalysisCompleted : Event {
        public const string Name = "analysisCompleted";

        public AnalysisCompleted() : base(Name) {
        }
    }

    public sealed class SignaturesResponse : Response {
        public Signature[] sigs;
    }

    public class Signature {
        public string name;
        public string doc;
        public Parameter[] parameters;
    }

    public class Parameter {
        public string name, defaultValue, doc, type;
        public bool optional;
    }

    public class AnalysisCompleteEvent : Event {
        public const string Name = "analysisComplete";
        public int fileId;

        public AnalysisCompleteEvent() : base(Name) {
        }
    }

    public class CompletionsResponse : Response {
        public Completion[] completions;
    }

    public class Completion {
        public string name;
        public string completion;
        public PythonMemberType memberType;
    }

    public sealed class OptionsChangedEvent : Event {
        public const string Name = "optionsChanged";

        public Severity indentation_inconsistency_severity;
        public bool implicitProject;

        public OptionsChangedEvent() : base(Name) {
        }
    }

    public sealed class TaskItem {
        public string message;
        public CodeSpan span;
        public TaskPriority priority;
        public TaskCategory category;
        public bool squiggle;
    }

    public enum TaskPriority {
        high,
        normal,
        low
    }

    public enum TaskCategory {
        buildCompile,
        comments
    }


}
