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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides access to an on-disk store of cached intellisense information.
    /// </summary>
    public sealed class PythonTypeDatabase {
        private readonly Dictionary<string, IPythonModule> _modules = new Dictionary<string, IPythonModule>();
        private readonly List<Action> _fixups = new List<Action>();
        private readonly string _dbDir;
        private readonly Dictionary<IPythonType, CPythonConstant> _constants = new Dictionary<IPythonType, CPythonConstant>();
        private readonly bool _is3x;
        private IBuiltinPythonModule _builtinModule;

        /// <summary>
        /// Gets the version of the analysis format that this class reads.
        /// </summary>
        public static readonly int CurrentVersion = 3;

        public PythonTypeDatabase(string databaseDirectory, bool is3x = false, IBuiltinPythonModule builtinsModule = null) {
            _dbDir = databaseDirectory;
            _modules["__builtin__"] = _builtinModule = builtinsModule ?? new CPythonBuiltinModule(this, "__builtin__", Path.Combine(databaseDirectory, is3x ? "builtins.idb" : "__builtin__.idb"), true);
            _is3x = is3x;

            foreach (var file in Directory.GetFiles(databaseDirectory)) {
                if (!file.EndsWith(".idb", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                } else if (String.Equals(Path.GetFileName(file), is3x ? "builtins.idb" : "__builtin__.idb", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string modName = Path.GetFileNameWithoutExtension(file);
                _modules[modName] = new CPythonModule(this, modName, file, false);
            }
        }

        public static PythonTypeDatabase CreateDefaultTypeDatabase() {
            return new PythonTypeDatabase(GetBaselineDatabasePath());
        }

        public IEnumerable<string> GetModuleNames() {
            return _modules.Keys;
        }

        public IPythonModule GetModule(string name) {
            IPythonModule res;
            if (_modules.TryGetValue(name, out res)) {
                return res;
            }
            return null;
        }

        public string DatabaseDirectory {
            get {
                return _dbDir;
            }
        }

        public IBuiltinPythonModule BuiltinModule {
            get {
                return _builtinModule;
            }
        }


        /// <summary>
        /// Creates a new completion database based upon the specified request.  Calls back the provided delegate when
        /// the generation has finished.
        /// </summary>
        public static bool Generate(PythonTypeDatabaseCreationRequest request, Action databaseGenerationCompleted) {
            if (String.IsNullOrEmpty(request.Factory.Configuration.InterpreterPath)) {
                return false;
            }

            string outPath = request.OutputPath;

            if (!Directory.Exists(outPath)) {
                Directory.CreateDirectory(outPath);
            }

            var psi = new ProcessStartInfo();
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.FileName = request.Factory.Configuration.InterpreterPath;
            psi.Arguments =
                "\"" + Path.Combine(GetPythonToolsInstallPath(), "PythonScraper.py") + "\"" +       // script to run
                " \"" + outPath + "\"" +                                                // output dir
                " \"" + GetBaselineDatabasePath() + "\"";           // baseline file

            var proc = new Process();
            proc.StartInfo = psi;
            try {
                LogEvent(request, "START_SCRAPE");

                proc.Start();
                proc.WaitForExit();
            } catch (Win32Exception ex) {
                // failed to start process, interpreter doesn't exist?           
                LogEvent(request, "FAIL_SCRAPE " + ex.ToString().Replace("\r\n", " -- "));
                return false;
            }

            if (proc.ExitCode != 0) {
                LogEvent(request, "FAIL_SCRAPE " + proc.ExitCode);
            }

            if ((proc.ExitCode == 0 || DatabaseExists(outPath)) && (request.DatabaseOptions & GenerateDatabaseOptions.StdLibDatabase) != 0) {
                Thread t = new Thread(x => {
                    psi = new ProcessStartInfo();
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.FileName = Path.Combine(GetPythonToolsInstallPath(), "Microsoft.PythonTools.Analyzer.exe");
                    if (File.Exists(psi.FileName)) {
                        psi.Arguments = "/dir " + "\"" + Path.Combine(Path.GetDirectoryName(request.Factory.Configuration.InterpreterPath), "Lib") + "\"" +
                            " /version V" + request.Factory.Configuration.Version.ToString().Replace(".", "") +
                            " /outdir " + "\"" + outPath + "\"" +
                            " /indir " + "\"" + outPath + "\"";

                        proc = new Process();
                        proc.StartInfo = psi;

                        try {
                            LogEvent(request, "START_STDLIB");
                            proc.Start();
                            proc.WaitForExit();

                            if (proc.ExitCode == 0) {
                                LogEvent(request, "DONE (STDLIB)");
                            } else {
                                LogEvent(request, "FAIL_STDLIB " + proc.ExitCode);
                            }
                        } catch (Win32Exception ex) {
                            // failed to start the process           
                            LogEvent(request, "FAIL_STDLIB " + ex.ToString().Replace("\r\n", " -- "));
                        }

                        databaseGenerationCompleted();
                    }
                });
                t.Start();
                return true;
            } else if (proc.ExitCode == 0) {
                LogEvent(request, "DONE (SCRAPE)");
                databaseGenerationCompleted();
            }
            return false;
        }

        private static bool DatabaseExists(string path) {
            string versionFile = Path.Combine(path, "database.ver");
            if (File.Exists(versionFile)) {
                try {
                    string allLines = File.ReadAllText(versionFile);
                    int version;
                    return Int32.TryParse(allLines, out version) && version == PythonTypeDatabase.CurrentVersion;
                } catch (IOException) {
                }
            }
            return false;
        }

        private static string GetLogFilename() {
            return Path.Combine(GetCompletionDatabaseDirPath(), "AnalysisLog.txt");
        }

        internal static string GetBaselineDatabasePath() {
            return Path.Combine(GetPythonToolsInstallPath(), "CompletionDB");
        }

        private static string GetCompletionDatabaseDirPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python Tools\\CompletionDB"
            );
        }

        private static void LogEvent(PythonTypeDatabaseCreationRequest request, string contents) {
            for (int i = 0; i < 10; i++) {
                try {
                    File.AppendAllText(
                        GetLogFilename(),
                        String.Format(
                            "\"{0}\" \"{1}\" \"{2}\" \"{3}\"{4}",
                            DateTime.Now.ToString("yyyy/MM/dd h:mm:ss.fff tt"),
                            request.Factory.Configuration.InterpreterPath,
                            request.OutputPath,
                            contents,
                            Environment.NewLine
                        )
                    );
                    return;
                } catch (IOException) {
                    // racing with someone else generating?
                    Thread.Sleep(25);
                }
            }
        }

        /// <summary>
        /// Looks up a type and queues a fixup if the type is not yet available.  Receives a delegate
        /// which assigns the value to the appropriate field.
        /// </summary>
        internal void LookupType(object type, Action<IPythonType> assign) {
            var value = LookupType(type);

            if (value == null) {
                AddFixup(
                    () => {
                        var delayedType = LookupType(type);
                        if (delayedType == null) {
                            delayedType = BuiltinModule.GetAnyMember("object") as IPythonType;
                        }
                        Debug.Assert(delayedType != null);
                        assign(delayedType);
                    }
                );
            } else {
                assign(value);
            }
        }

        private IPythonType LookupType(object type) {
            if (type != null) {
                object[] typeInfo = (object[])type;
                if (typeInfo.Length == 2) {
                    string modName = typeInfo[0] as string;
                    string typeName = typeInfo[1] as string;

                    if (modName != null) {
                        if (typeName != null) {
                            var module = GetModule(modName);
                            if (module != null) {
                                IBuiltinPythonModule builtin = module as IBuiltinPythonModule;
                                if (builtin != null) {
                                    return builtin.GetAnyMember(typeName) as IPythonType;
                                }
                                return module.GetMember(null, typeName) as IPythonType;
                            }
                        }
                    }
                }
            } else {
                return BuiltinModule.GetAnyMember("object") as IPythonType;
            }
            return null;
        }

        internal string GetBuiltinTypeName(BuiltinTypeId id) {
            string name;
            switch (id) {
                case BuiltinTypeId.Bool: name = "bool"; break;
                case BuiltinTypeId.Complex: name = "complex"; break;
                case BuiltinTypeId.Dict: name = "dict"; break;
                case BuiltinTypeId.Float: name = "float"; break;
                case BuiltinTypeId.Int: name = "int"; break;
                case BuiltinTypeId.List: name = "list"; break;
                case BuiltinTypeId.Long: name = "long"; break;
                case BuiltinTypeId.Object: name = "object"; break;
                case BuiltinTypeId.Set: name = "set"; break;
                case BuiltinTypeId.Str:
                    if (_is3x) {
                        name = "str";
                    } else {
                        name = "unicode";
                    }
                    break;
                case BuiltinTypeId.Bytes:
                    if (_is3x) {
                        name = "bytes";
                    } else {
                        name = "str";
                    }
                    break;
                case BuiltinTypeId.Tuple: name = "tuple"; break;
                case BuiltinTypeId.Type: name = "type"; break;

                case BuiltinTypeId.BuiltinFunction: name = "builtin_function"; break;
                case BuiltinTypeId.BuiltinMethodDescriptor: name = "builtin_method_descriptor"; break;
                case BuiltinTypeId.DictKeys: name = "dict_keys"; break;
                case BuiltinTypeId.DictValues: name = "dict_values"; break;
                case BuiltinTypeId.Function: name = "function"; break;
                case BuiltinTypeId.Generator: name = "generator"; break;
                case BuiltinTypeId.NoneType: name = "NoneType"; break;
                case BuiltinTypeId.Ellipsis: name = "ellipsis"; break;

                default: return null;
            }
            return name;
        }

        /// <summary>
        /// Adds a custom action which will attempt to resolve a type lookup which failed because the
        /// type was not yet defined.  All fixups are run after the database is loaded so all types
        /// should be available.
        /// </summary>
        private void AddFixup(Action action) {
            _fixups.Add(action);
        }

        /// <summary>
        /// Runs all of the custom fixup actions.
        /// </summary>
        internal void RunFixups() {
            foreach (var fixup in _fixups) {
                fixup();
            }

            _fixups.Clear();
        }
        
        internal void ReadMember(string memberName, Dictionary<string, object> memberValue, Action<string, IMember> assign, IMemberContainer container) {
            object memberKind;
            object value;
            Dictionary<string, object> valueDict;

            if (memberValue.TryGetValue("value", out value) &&
                (valueDict = (value as Dictionary<string, object>)) != null &&
                memberValue.TryGetValue("kind", out memberKind) && memberKind is string) {
                switch ((string)memberKind) {
                    case "function":
                        assign(memberName, new CPythonFunction(this, memberName, valueDict, container));
                        break;
                    case "func_ref":
                        string funcName;
                        if (valueDict.TryGetValue("func_name", out value) && (funcName = value as string) != null) {
                            var names = funcName.Split('.');
                            IPythonModule mod;
                            if (this._modules.TryGetValue(names[0], out mod)) {
                                if (names.Length == 2) {
                                    var mem = mod.GetMember(null, names[1]);
                                    if (mem == null) {
                                        AddFixup(() => {
                                            var tmp = mod.GetMember(null, names[1]);
                                            if (tmp != null) {
                                                assign(memberName, tmp);
                                            }
                                        });
                                    } else {
                                        assign(memberName, mem);
                                    }
                                } else {
                                    LookupType(new object[] { names[0], names[1] }, (type) => {
                                        var mem = type.GetMember(null, names[2]);
                                        if (mem != null) {
                                            assign(memberName, mem);
                                        }
                                    });
                                }
                            }
                        }
                        break;
                    case "method":
                        assign(memberName, new CPythonMethodDescriptor(this, memberName, valueDict, container));
                        break;
                    case "property":
                        assign(memberName, new CPythonProperty(this, valueDict));
                        break;
                    case "data":
                        object typeInfo;
                        if (valueDict.TryGetValue("type", out typeInfo)) {
                            LookupType(typeInfo, (dataType) => {
                                assign(memberName, GetConstant(dataType));
                            });
                        }
                        break;
                    case "type":
                        assign(memberName, MakeType(memberName, valueDict, container));
                        break;
                    case "multiple":
                        object members;
                        object[] memsArray;
                        if (valueDict.TryGetValue("members", out members) && (memsArray = members as object[]) != null) {
                            IMember[] finalMembers = GetMultipleMembers(memberName, container, memsArray);
                            assign(memberName, new CPythonMultipleMembers(finalMembers));
                        }
                        break;
                    case "typeref":
                        object typeName;
                        if (valueDict.TryGetValue("type_name", out typeName)) {
                            LookupType(typeName, (dataType) => {
                                assign(memberName, dataType);
                            });
                        }
                        break;
                    case "moduleref":
                        object modName;
                        if (!valueDict.TryGetValue("module_name", out modName) || !(modName is string)) {
                            throw new InvalidOperationException("Failed to find module name: " + modName);
                        }

                        assign(memberName, GetModule((string)modName));
                        break;
                }
            }
        }

        private IMember[] GetMultipleMembers(string memberName, IMemberContainer container, object[] memsArray) {
            IMember[] finalMembers = new IMember[memsArray.Length];
            for (int i = 0; i < finalMembers.Length; i++) {
                var curMember = memsArray[i] as Dictionary<string, object>;
                var tmp = i;    // close over the current value of i, not the last one...
                if (curMember != null) {
                    ReadMember(memberName, curMember, (name, newMemberValue) => finalMembers[tmp] = newMemberValue, container);
                }
            }
            return finalMembers;
        }

        private CPythonType MakeType(string typeName, Dictionary<string, object> valueDict, IMemberContainer container) {
            BuiltinTypeId typeId = BuiltinTypeId.Unknown;
            if (container == _builtinModule) {
                typeId = GetBuiltinTypeId(typeName);
            }

            return new CPythonType(container, this, typeName, valueDict, typeId);
        }

        private BuiltinTypeId GetBuiltinTypeId(string typeName) {
            switch (typeName) {
                case "list": return BuiltinTypeId.List;
                case "tuple": return BuiltinTypeId.Tuple;
                case "float": return BuiltinTypeId.Float;
                case "int": return BuiltinTypeId.Int;
                case "complex": return BuiltinTypeId.Complex;
                case "dict": return BuiltinTypeId.Dict;
                case "bool": return BuiltinTypeId.Bool;
                case "generator": return BuiltinTypeId.Generator;
                case "function": return BuiltinTypeId.Function;
                case "set": return BuiltinTypeId.Set;
                case "type": return BuiltinTypeId.Type;
                case "object": return BuiltinTypeId.Object;
                case "long": return BuiltinTypeId.Long;
                case "str":
                    if (_is3x) {
                        return BuiltinTypeId.Str;
                    }
                    return BuiltinTypeId.Bytes;
                case "unicode":
                    return BuiltinTypeId.Str;
                case "bytes":
                    return BuiltinTypeId.Bytes;
                case "builtin_function": return BuiltinTypeId.BuiltinFunction;
                case "builtin_method_descriptor": return BuiltinTypeId.BuiltinMethodDescriptor;
                case "NoneType": return BuiltinTypeId.NoneType;
                case "ellipsis": return BuiltinTypeId.Ellipsis;
                case "dict_keys": return BuiltinTypeId.DictKeys;
                case "dict_values": return BuiltinTypeId.DictValues;
            }
            return BuiltinTypeId.Unknown;
        }

        internal CPythonConstant GetConstant(IPythonType type) {
            CPythonConstant constant;
            if (!_constants.TryGetValue(type, out constant)) {
                _constants[type] = constant = new CPythonConstant(type);
            }
            return constant;
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        private static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "Microsoft.PythonTools.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\1.0");
                    if (File.Exists(Path.Combine(toolsPath, "Microsoft.PythonTools.dll"))) {
                        return toolsPath;
                    }
                }
            }

            return null;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (Environment.Is64BitOperatingSystem) {
#if DEV11
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            } else {
#if DEV11
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#else
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#endif
            }
        }
    }
}
