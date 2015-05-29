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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudioTools;
using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.Interpreter {
    public sealed class MSBuildProjectInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        readonly IInterpreterOptionsService _service;
        readonly MSBuild.Project _project;
        readonly Dictionary<Guid, string> _rootPaths;

        readonly object _factoriesLock = new object();
        Dictionary<IPythonInterpreterFactory, FactoryInfo> _factories;

        IPythonInterpreterFactory _active;

        // keys used for storing information about user defined interpreters
        public const string InterpreterItem = "Interpreter";
        public const string IdKey = "Id";
        public const string InterpreterPathKey = "InterpreterPath";
        public const string WindowsPathKey = "WindowsInterpreterPath";
        public const string LibraryPathKey = "LibraryPath";
        public const string ArchitectureKey = "Architecture";
        public const string VersionKey = "Version";
        public const string PathEnvVarKey = "PathEnvironmentVariable";
        public const string DescriptionKey = "Description";
        public const string BaseInterpreterKey = "BaseInterpreter";

        public const string InterpreterReferenceItem = "InterpreterReference";
        private static readonly Regex InterpreterReferencePath = new Regex(
            @"\{?(?<id>[a-f0-9\-]+)\}?
              \\
              (?<version>[23]\.[0-9])",
            RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase
        );

        internal const string InterpreterIdProperty = "InterpreterId";
        internal const string InterpreterVersionProperty = "InterpreterVersion";

        /// <summary>
        /// Creates a new provider for the specified project and service.
        /// </summary>
        public MSBuildProjectInterpreterFactoryProvider(IInterpreterOptionsService service, MSBuild.Project project) {
            if (service == null) {
                throw new ArgumentNullException("service");
            }
            if (project == null) {
                throw new ArgumentNullException("project");
            }

            _rootPaths = new Dictionary<Guid, string>();
            _service = service;
            _project = project;

            // _active starts as null, so we need to start with this event
            // hooked up.
            _service.DefaultInterpreterChanged += GlobalDefaultInterpreterChanged;
        }

        /// <summary>
        /// Call to find interpreters in the associated project. Separated from
        /// the constructor to allow exceptions to be handled without causing
        /// the project node to be invalid.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// One or more interpreters failed to load. The error message should be
        /// presented to the user, but can otherwise be ignored.
        /// </exception>
        public void DiscoverInterpreters() {
            // <Interpreter Include="InterpreterDirectory">
            //   <Id>guid</Id>
            //   <BaseInterpreter>guid</BaseInterpreter>
            //   <Version>...</Version>
            //   <InterpreterPath>...</InterpreterPath>
            //   <WindowsInterpreterPath>...</WindowsInterpreterPath>
            //   <LibraryPath>...</LibraryPath>
            //   <PathEnvironmentVariable>...</PathEnvironmentVariable>
            //   <Description>...</Description>
            // </Interpreter>

            var errors = new StringBuilder();
            errors.AppendLine("Some project interpreters failed to load:");
            bool anyChange = false, anyError = false;

            var projectHome = CommonUtils.GetAbsoluteDirectoryPath(_project.DirectoryPath, _project.GetPropertyValue("ProjectHome"));
            var factories = new Dictionary<IPythonInterpreterFactory, FactoryInfo>();
            foreach (var item in _project.GetItems(InterpreterItem)) {
                IPythonInterpreterFactory fact;
                Guid id, baseId;

                // Errors in these options are fatal, so we set anyError and
                // continue with the next entry.
                var dir = item.EvaluatedInclude;
                if (!CommonUtils.IsValidPath(dir)) {
                    errors.AppendLine(string.Format("Interpreter has invalid path: {0}", dir ?? "(null)"));
                    anyError = true;
                    continue;
                }
                dir = CommonUtils.GetAbsoluteDirectoryPath(projectHome, dir);

                var value = item.GetMetadataValue(IdKey);
                if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out id)) {
                    errors.AppendLine(string.Format("Interpreter {0} has invalid value for '{1}': {2}", dir, IdKey, value));
                    anyError = true;
                    continue;
                }
                if (factories.Keys.Any(f => f.Id == id)) {
                    errors.AppendLine(string.Format("Interpreter {0} has a non-unique id: {1}", dir, id));
                    continue;
                }

                var verStr = item.GetMetadataValue(VersionKey);
                Version ver;
                if (string.IsNullOrEmpty(verStr) || !Version.TryParse(verStr, out ver)) {
                    errors.AppendLine(string.Format("Interpreter {0} has invalid value for '{1}': {2}", dir, VersionKey, verStr));
                    anyError = true;
                    continue;
                }

                // The rest of the options are non-fatal. We create an instance
                // of NotFoundError with an amended description, which will
                // allow the user to remove the entry from the project file
                // later.
                bool hasError = false;

                var description = item.GetMetadataValue(DescriptionKey);
                if (string.IsNullOrEmpty(description)) {
                    description = CommonUtils.CreateFriendlyDirectoryPath(projectHome, dir);
                }

                value = item.GetMetadataValue(BaseInterpreterKey);
                PythonInterpreterFactoryWithDatabase baseInterp = null;
                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out baseId)) {
                    // It's a valid GUID, so find a suitable base. If we
                    // don't find one now, we'll try and figure it out from
                    // the pyvenv.cfg/orig-prefix.txt files later.
                    // Using an empty GUID will always go straight to the
                    // later lookup.
                    if (baseId != Guid.Empty) {
                        baseInterp = _service.FindInterpreter(baseId, ver) as PythonInterpreterFactoryWithDatabase;
                    }
                }

                var path = item.GetMetadataValue(InterpreterPathKey);
                if (!CommonUtils.IsValidPath(path)) {
                    errors.AppendLine(string.Format("Interpreter {0} has invalid value for '{1}': {2}", dir, InterpreterPathKey, path));
                    hasError = true;
                } else if (!hasError) {
                    path = CommonUtils.GetAbsoluteFilePath(dir, path);
                }

                var winPath = item.GetMetadataValue(WindowsPathKey);
                if (!CommonUtils.IsValidPath(winPath)) {
                    errors.AppendLine(string.Format("Interpreter {0} has invalid value for '{1}': {2}", dir, WindowsPathKey, winPath));
                    hasError = true;
                } else if (!hasError) {
                    winPath = CommonUtils.GetAbsoluteFilePath(dir, winPath);
                }

                var libPath = item.GetMetadataValue(LibraryPathKey);
                if (string.IsNullOrEmpty(libPath)) {
                    libPath = "lib";
                }
                if (!CommonUtils.IsValidPath(libPath)) {
                    errors.AppendLine(string.Format("Interpreter {0} has invalid value for '{1}': {2}", dir, LibraryPathKey, libPath));
                    hasError = true;
                } else if (!hasError) {
                    libPath = CommonUtils.GetAbsoluteDirectoryPath(dir, libPath);
                }

                var pathVar = item.GetMetadataValue(PathEnvVarKey);
                if (string.IsNullOrEmpty(pathVar)) {
                    if (baseInterp != null) {
                        pathVar = baseInterp.Configuration.PathEnvironmentVariable;
                    } else {
                        pathVar = "PYTHONPATH";
                    }
                }

                string arch = null;
                if (baseInterp == null) {
                    arch = item.GetMetadataValue(ArchitectureKey);
                    if (string.IsNullOrEmpty(arch)) {
                        arch = "x86";
                    }
                }

                if (baseInterp == null && !hasError) {
                    // Only thing missing is the base interpreter, so let's try
                    // to find it using paths
                    baseInterp = DerivedInterpreterFactory.FindBaseInterpreterFromVirtualEnv(dir, libPath, _service) as
                        PythonInterpreterFactoryWithDatabase;

                    if (baseInterp == null) {
                        errors.AppendLine(string.Format("Interpreter {0} has invalid value for '{1}': {2}", dir, BaseInterpreterKey, value ?? "(null)"));
                        hasError = true;
                    }
                }

                if (hasError) {
                    fact = new NotFoundInterpreterFactory(
                        id,
                        ver,
                        string.Format("{0} (unavailable)", description),
                        Directory.Exists(dir) ? dir : null
                    );
                } else if (baseInterp != null) {
                    MigrateOldDerivedInterpreterFactoryDatabase(id, baseInterp.Configuration.Version, dir);
                    fact = new DerivedInterpreterFactory(
                        baseInterp,
                        new InterpreterFactoryCreationOptions {
                            LanguageVersion = baseInterp.Configuration.Version,
                            Id = id,
                            Description = description,
                            InterpreterPath = path,
                            WindowInterpreterPath = winPath,
                            LibraryPath = libPath,
                            PrefixPath = dir,
                            PathEnvironmentVariableName = pathVar,
                            Architecture = baseInterp.Configuration.Architecture,
                            WatchLibraryForNewModules = true
                        }
                    );
                } else {
                    fact = InterpreterFactoryCreator.CreateInterpreterFactory(new InterpreterFactoryCreationOptions {
                        LanguageVersion = ver,
                        Id = id,
                        Description = description,
                        InterpreterPath = path,
                        WindowInterpreterPath = winPath,
                        LibraryPath = libPath,
                        PrefixPath = dir,
                        PathEnvironmentVariableName = pathVar,
                        ArchitectureString = arch,
                        WatchLibraryForNewModules = true
                    });
                }
                var existing = FindInterpreter(id, ver);
                if (existing != null && existing.IsEqual(fact)) {
                    factories[existing] = new FactoryInfo(item, factories[existing].Owned);
                    var disposable = fact as IDisposable;
                    if (disposable != null) {
                        disposable.Dispose();
                    }
                } else {
                    _rootPaths[id] = dir;
                    factories[fact] = new FactoryInfo(item, true);
                    anyChange = true;
                }
            }

            // <InterpreterReference Include="{guid};{version}" />
            foreach (var item in _project.GetItems(InterpreterReferenceItem)) {
                var match = InterpreterReferencePath.Match(item.EvaluatedInclude);
                if (match == null || !match.Success || !match.Groups.Cast<Group>().All(g => g.Success)) {
                    errors.AppendLine(string.Format("Interpreter reference has invalid path: {0}", item.EvaluatedInclude));
                    anyError = true;
                    continue;
                }
                Guid id;
                var value = match.Groups["id"];
                if (string.IsNullOrEmpty(value.Value) || !Guid.TryParse(value.Value, out id)) {
                    errors.AppendLine(string.Format("Interpreter reference has invalid id: {0}", value.Value ?? "(null)"));
                    anyError = true;
                    continue;
                }
                Version ver;
                value = match.Groups["version"];
                if (string.IsNullOrEmpty(value.Value) || !Version.TryParse(value.Value, out ver)) {
                    errors.AppendLine(string.Format("Interpreter reference has invalid version: {0}", value.Value ?? "(null)"));
                    anyError = true;
                    continue;
                }

                bool owned = false;
                var fact = _service.FindInterpreter(id, ver);
                if (fact == null) {
                    owned = true;
                    fact = new NotFoundInterpreterFactory(id, ver);
                }

                var existing = FindInterpreter(id, ver);
                if (existing != null) {
                    factories[existing] = new FactoryInfo(item, factories[existing].Owned);
                    if (owned) {
                        ((PythonInterpreterFactoryWithDatabase)fact).Dispose();
                    }
                } else {
                    factories[fact] = new FactoryInfo(item, owned);
                    anyChange = true;
                }
            }

            if (anyChange || _factories == null || factories.Count != _factories.Count) {
                // Lock here mainly to ensure that any searches complete before
                // we trigger the changed event.
                lock (_factoriesLock) {
                    _factories = factories;
                }
                OnInterpreterFactoriesChanged();
                UpdateActiveInterpreter();
            }

            if (anyError) {
                throw new InvalidDataException(errors.ToString());
            }
        }

        /// <summary>
        /// Creates a derived interpreter factory from the specified set of
        /// options. This function will modify the project, raise the
        /// <see cref="InterpreterFactoriesChanged"/> event and potentially
        /// display UI.
        /// </summary>
        /// <param name="options">
        /// <para>The options for the new interpreter:</para>
        /// <para>Guid: ID of the base interpreter.</para>
        /// <para>Version: Version of the base interpreter. This will also be
        /// the version of the new interpreter.</para>
        /// <para>PythonPath: Either the path to the root of the virtual
        /// environment, or directly to the interpreter executable. If no file
        /// exists at the provided path, the name of the interpreter specified
        /// for the base interpreter is tried. If that is not found, "scripts"
        /// is added as the last directory. If that is not found, an exception
        /// is raised.</para>
        /// <para>PythonWindowsPath [optional]: The path to the interpreter
        /// executable for windowed applications. If omitted, an executable with
        /// the same name as the base interpreter's will be used if it exists.
        /// Otherwise, this will be set to the same as PythonPath.</para>
        /// <para>PathEnvVar [optional]: The name of the environment variable to
        /// set for search paths. If omitted, the value from the base
        /// interpreter will be used.</para>
        /// <para>Description [optional]: The user-friendly name of the
        /// interpreter. If omitted, the relative path from the project home to
        /// the directory containing the interpreter is used. If this path ends
        /// in "\\Scripts", the last segment is also removed.</para>
        /// </param>
        /// <returns>The ID of the created interpreter.</returns>
        public Guid CreateInterpreterFactory(InterpreterFactoryCreationOptions options) {
            var projectHome = CommonUtils.GetAbsoluteDirectoryPath(_project.DirectoryPath, _project.GetPropertyValue("ProjectHome"));
            var rootPath = CommonUtils.GetAbsoluteDirectoryPath(projectHome, options.PrefixPath);

            IPythonInterpreterFactory fact;
            var id = Guid.NewGuid();
            var baseInterp = _service.FindInterpreter(options.Id, options.LanguageVersion)
                as PythonInterpreterFactoryWithDatabase;
            if (baseInterp != null) {
                var pathVar = options.PathEnvironmentVariableName;
                if (string.IsNullOrEmpty(pathVar)) {
                    pathVar = baseInterp.Configuration.PathEnvironmentVariable;
                }

                var description = options.Description;
                if (string.IsNullOrEmpty(description)) {
                    description = CommonUtils.CreateFriendlyDirectoryPath(projectHome, rootPath);
                    int i = description.LastIndexOf("\\scripts", StringComparison.OrdinalIgnoreCase);
                    if (i > 0) {
                        description = description.Remove(i);
                    }
                }

                MigrateOldDerivedInterpreterFactoryDatabase(id, baseInterp.Configuration.Version, options.PrefixPath);
                fact = new DerivedInterpreterFactory(
                    baseInterp,
                    new InterpreterFactoryCreationOptions {
                        Id = id,
                        LanguageVersion = baseInterp.Configuration.Version,
                        Description = description,
                        InterpreterPath = options.InterpreterPath,
                        WindowInterpreterPath = options.WindowInterpreterPath,
                        LibraryPath = options.LibraryPath,
                        PrefixPath = options.PrefixPath,
                        PathEnvironmentVariableName = pathVar,
                        Architecture = baseInterp.Configuration.Architecture,
                        WatchLibraryForNewModules = true
                    }
                );
            } else {
                fact = InterpreterFactoryCreator.CreateInterpreterFactory(
                    new InterpreterFactoryCreationOptions {
                        Id = id,
                        LanguageVersion = options.LanguageVersion,
                        Description = options.Description,
                        InterpreterPath = options.InterpreterPath,
                        WindowInterpreterPath = options.WindowInterpreterPath,
                        LibraryPath = options.LibraryPath,
                        PrefixPath = options.PrefixPath,
                        PathEnvironmentVariableName = options.PathEnvironmentVariableName,
                        Architecture = options.Architecture,
                        WatchLibraryForNewModules = options.WatchLibraryForNewModules
                    }
                );
            }

            AddInterpreter(fact, true);

            return id;
        }

        private static void MigrateOldDerivedInterpreterFactoryDatabase(Guid id, Version version, string prefixPath) {
            var newPath = Path.Combine(prefixPath, ".ptvs");
            var oldPath = Path.Combine(newPath, id.ToString(), version.ToString());
            if (Directory.Exists(oldPath)) {
                bool success = false;
                try {
                    foreach (var file in Directory.GetFiles(oldPath, "*", SearchOption.AllDirectories)) {
                        var newFile = CommonUtils.GetAbsoluteFilePath(newPath, CommonUtils.GetRelativeFilePath(oldPath, file));
                        var newDirectory = Path.GetDirectoryName(newFile);
                        Directory.CreateDirectory(newDirectory);
                        File.Move(file, newFile);
                    }
                    success = true;
                } catch (ArgumentException) {
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }

                try {
                    if (success) {
                        // Succeeded, so just delete the old database folders
                        Directory.Delete(Path.Combine(newPath, id.ToString()), true);
                    } else {
                        // Failed, so delete everything. The DB will regenerate.
                        Directory.Delete(newPath, true);
                    }
                } catch (ArgumentException) {
                } catch (IOException) {
                } catch (UnauthorizedAccessException) {
                }
            }

        }

        /// <summary>
        /// Adds the specified factory to the project. If the factory was
        /// created by this provider, it will be added as an Interpreter element
        /// with full details. If the factory was not created by this provider,
        /// it will be added as an InterpreterReference element with only the
        /// ID and version.
        /// </summary>
        /// <param name="factory">The factory to add.</param>
        public void AddInterpreter(IPythonInterpreterFactory factory, bool disposeInterpreter = false) {
            if (factory == null) {
                throw new ArgumentNullException("factory");
            }

            if (_factories.ContainsKey(factory)) {
                return;
            }

            MSBuild.ProjectItem item;

            var derived = factory as DerivedInterpreterFactory;
            if (derived != null) {
                var projectHome = CommonUtils.GetAbsoluteDirectoryPath(_project.DirectoryPath, _project.GetPropertyValue("ProjectHome"));
                var rootPath = CommonUtils.EnsureEndSeparator(factory.Configuration.PrefixPath);
                _rootPaths[factory.Id] = rootPath;

                item = _project.AddItem(InterpreterItem,
                    CommonUtils.GetRelativeDirectoryPath(projectHome, rootPath),
                    new Dictionary<string, string> {
                        { IdKey, derived.Id.ToString("B") },
                        { BaseInterpreterKey, derived.BaseInterpreter.Id.ToString("B") },
                        { VersionKey, derived.BaseInterpreter.Configuration.Version.ToString() },
                        { DescriptionKey, derived.Description },
                        { InterpreterPathKey, CommonUtils.GetRelativeFilePath(rootPath, derived.Configuration.InterpreterPath) },
                        { WindowsPathKey, CommonUtils.GetRelativeFilePath(rootPath, derived.Configuration.WindowsInterpreterPath) },
                        { LibraryPathKey, CommonUtils.GetRelativeDirectoryPath(rootPath, derived.Configuration.LibraryPath) },
                        { PathEnvVarKey, derived.Configuration.PathEnvironmentVariable },
                        { ArchitectureKey, derived.Configuration.Architecture.ToString() }
                    }).FirstOrDefault();
            } else if (_service.FindInterpreter(factory.Id, factory.Configuration.Version) != null) {
                // The interpreter exists globally, so add a reference.
                item = _project.AddItem(InterpreterReferenceItem,
                    string.Format("{0:B}\\{1}", factory.Id, factory.Configuration.Version)
                    ).FirstOrDefault();
            } else {
                // Can't find the interpreter anywhere else, so add its
                // configuration to the project file.
                var projectHome = CommonUtils.GetAbsoluteDirectoryPath(_project.DirectoryPath, _project.GetPropertyValue("ProjectHome"));
                var rootPath = CommonUtils.EnsureEndSeparator(factory.Configuration.PrefixPath);

                item = _project.AddItem(InterpreterItem,
                    CommonUtils.GetRelativeDirectoryPath(projectHome, rootPath),
                    new Dictionary<string, string> {
                        { IdKey, factory.Id.ToString("B") },
                        { VersionKey, factory.Configuration.Version.ToString() },
                        { DescriptionKey, factory.Description },
                        { InterpreterPathKey, CommonUtils.GetRelativeFilePath(rootPath, factory.Configuration.InterpreterPath) },
                        { WindowsPathKey, CommonUtils.GetRelativeFilePath(rootPath, factory.Configuration.WindowsInterpreterPath) },
                        { LibraryPathKey, CommonUtils.GetRelativeDirectoryPath(rootPath, factory.Configuration.LibraryPath) },
                        { PathEnvVarKey, factory.Configuration.PathEnvironmentVariable },
                        { ArchitectureKey, factory.Configuration.Architecture.ToString() }
                    }).FirstOrDefault();
            }

            lock (_factoriesLock) {
                _factories[factory] = new FactoryInfo(item, disposeInterpreter);
            }
            OnInterpreterFactoriesChanged();
            UpdateActiveInterpreter();
        }

        /// <summary>
        /// Removes an interpreter factory from the project. This function will
        /// modify the project, but does not handle source control.
        /// </summary>
        /// <param name="factory">
        /// The id of the factory to remove. The function returns silently if
        /// the factory is not known by this provider.
        /// </param>
        public void RemoveInterpreterFactory(IPythonInterpreterFactory factory) {
            if (factory == null) {
                throw new ArgumentNullException("factory");
            }

            if (!_factories.ContainsKey(factory)) {
                return;
            }

            string rootPath;
            if (_rootPaths.TryGetValue(factory.Id, out rootPath)) {
                _rootPaths.Remove(factory.Id);
                foreach (var item in _project.GetItems(InterpreterItem)) {
                    Guid itemId;
                    if (Guid.TryParse(item.GetMetadataValue(IdKey), out itemId) && factory.Id == itemId) {
                        _project.RemoveItem(item);
                        _project.MarkDirty();
                        break;
                    }
                }
            } else {
                foreach (var item in _project.GetItems(InterpreterReferenceItem)) {
                    Guid itemId;
                    Version itemVer;
                    var match = InterpreterReferencePath.Match(item.EvaluatedInclude);
                    if (match != null && match.Success && match.Groups.Cast<Group>().All(g => g.Success) &&
                        Guid.TryParse(match.Groups["id"].Value, out itemId) &&
                        Version.TryParse(match.Groups["version"].Value, out itemVer) &&
                        factory.Id == itemId &&
                        factory.Configuration.Version == itemVer) {
                        _project.RemoveItem(item);
                        _project.MarkDirty();
                        break;
                    }
                }
            }

            bool raiseEvent;
            FactoryInfo factInfo;
            lock (_factoriesLock) {
                raiseEvent = _factories.TryGetValue(factory, out factInfo) && _factories.Remove(factory);
            }
            if (factInfo != null &&
                factInfo.Owned &&
                factory is IDisposable) {
                ((IDisposable)factory).Dispose();
            }
            UpdateActiveInterpreter();
            if (raiseEvent) {
                OnInterpreterFactoriesChanged();
            }
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            lock (_factoriesLock) {
                if (_factories != null) {
                    return _factories.Keys.ToList();
                }
            }
            return Enumerable.Empty<IPythonInterpreterFactory>();
        }

        public IEnumerable<IPythonInterpreterFactory> GetProjectSpecificInterpreterFactories() {
            lock (_factoriesLock) {
                if (_factories != null) {
                    return _factories
                        .Where(kv => kv.Value != null && kv.Value.ProjectItem != null &&
                            kv.Value.ProjectItem.ItemType.Equals(InterpreterItem))
                        .Select(kv => kv.Key)
                        .ToList();
                }
            }
            return Enumerable.Empty<IPythonInterpreterFactory>();
        }

        /// <summary>
        /// Returns the MSBuild project item that specified the provided
        /// interpreter.
        /// </summary>
        public MSBuild.ProjectItem GetProjectItem(IPythonInterpreterFactory factory) {
            lock (_factoriesLock) {
                FactoryInfo info;
                if (_factories.TryGetValue(factory, out info)) {
                    return info.ProjectItem;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns true if the provided interpreter was specified in the
        /// project (as opposed to included by reference).
        /// </summary>
        /// <remarks>
        /// This will always return false if <see cref="Contains"/> would return
        /// false.
        /// </remarks>
        public bool IsProjectSpecific(IPythonInterpreterFactory factory) {
            lock (_factoriesLock) {
                FactoryInfo info;
                if (_factories.TryGetValue(factory, out info)) {
                    return info.ProjectItem.ItemType.Equals(InterpreterItem);
                }
            }
            // We don't contain the interpreter, so it's not specific to this
            // project.
            return false;
        }

        /// <summary>
        /// Returns true if the provided interpreter was resolved when it was
        /// loaded.
        /// </summary>
        /// <remarks>
        /// This may return true even if <see cref="Contains"/> would return
        /// false for the same interpreter.
        /// </remarks>
        public bool IsAvailable(IPythonInterpreterFactory factory) {
            return factory != null &&
                !(factory is NotFoundInterpreterFactory) &&
                factory.Configuration != null;
        }

        /// <summary>
        /// Returns true if the provided interpreter is available to this
        /// project. 
        /// </summary>
        /// <remarks>
        /// If the project did not specify any interpreters, the global default
        /// interpreter is available, but this function will still return false.
        /// </remarks>
        public bool Contains(IPythonInterpreterFactory factory) {
            lock (_factoriesLock) {
                return _factories.ContainsKey(factory);
            }
        }

        public IPythonInterpreterFactory FindInterpreter(string rootPath) {
            var projectHome = CommonUtils.GetAbsoluteDirectoryPath(
                _project.DirectoryPath,
                _project.GetPropertyValue("ProjectHome")
            );
            rootPath = CommonUtils.GetAbsoluteDirectoryPath(projectHome, rootPath);

            foreach (var kv in _rootPaths) {
                if (kv.Value.Equals(rootPath, StringComparison.OrdinalIgnoreCase)) {
                    return FindInterpreter(kv.Key, new Version());
                }
            }
            return null;
        }

        public IPythonInterpreterFactory FindInterpreter(Guid id, Version version) {
            lock (_factoriesLock) {
                if (_factories != null) {
                    foreach (var fact in _factories.Keys) {
                        if (fact.Id == id && (version.Major == 0 || fact.Configuration.Version == version)) {
                            return fact;
                        }
                    }
                }
            }
            return null;
        }

        public IPythonInterpreterFactory FindInterpreter(Guid id, string version) {
            Version v;
            if (Version.TryParse(version, out v)) {
                return FindInterpreter(id, v);
            }
            return null;
        }

        public IPythonInterpreterFactory FindInterpreter(string id, string version) {
            Guid g;
            if (Guid.TryParse(id, out g)) {
                return FindInterpreter(g, version);
            }
            return null;
        }

        public event EventHandler InterpreterFactoriesChanged;

        private void OnInterpreterFactoriesChanged() {
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        private void GlobalDefaultInterpreterChanged(object sender, EventArgs e) {
            // This event is only raised when our active interpreter is the
            // global default.
            var evt = ActiveInterpreterChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        private void UpdateActiveInterpreter() {
            lock (_factoriesLock) {
                var newActive = _active;
                if (newActive == null || _factories == null || !_factories.ContainsKey(newActive)) {
                    newActive = FindInterpreter(
                        _project.GetPropertyValue(InterpreterIdProperty),
                        _project.GetPropertyValue(InterpreterVersionProperty)
                    );
                }
                ActiveInterpreter = newActive;
            }
        }

        /// <summary>
        /// Gets or sets the interpreter factory to use for this project. This
        /// will never return null, and will only return interpreters associated
        /// with the project or the global default. Setting this property will
        /// modify the project file, but does not modify source control.
        /// </summary>
        public IPythonInterpreterFactory ActiveInterpreter {
            get {
                return _active ?? _service.DefaultInterpreter;
            }
            set {
                var oldActive = _active;

                lock (_factoriesLock) {
                    if (_factories == null || !_factories.Any()) {
                        // No factories, so we must use the global default.
                        _active = null;
                    } else if (value == null || !_factories.ContainsKey(value)) {
                        // Choose a factory and make it our default.
                        _active = _factories.Keys
                            .Where(f => !(f is NotFoundInterpreterFactory))
                            .Where(f => !string.IsNullOrEmpty(f.Configuration.InterpreterPath))
                            .OrderBy(f => f.Description)
                            .ThenBy(f => f.Configuration.Version)
                            .LastOrDefault();
                    } else {
                        _active = value;
                    }
                }

                if (_active != oldActive) {
                    if (oldActive == null) {
                        // No longer need to listen to this event
                        _service.DefaultInterpreterChanged -= GlobalDefaultInterpreterChanged;
                    }

                    if (_active != null) {
                        _project.SetProperty(InterpreterIdProperty, _active.Id.ToString("B"));
                        _project.SetProperty(InterpreterVersionProperty, _active.Configuration.Version.ToString());
                    } else {
                        _project.SetProperty(InterpreterIdProperty, "");
                        _project.SetProperty(InterpreterVersionProperty, "");
                        // Need to start listening to this event
                        _service.DefaultInterpreterChanged += GlobalDefaultInterpreterChanged;
                    }
                    _project.MarkDirty();

                    var evt = ActiveInterpreterChanged;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }
                }
            }
        }

        public bool IsActiveInterpreterGlobalDefault {
            get {
                return _active == null;
            }
        }

        public event EventHandler ActiveInterpreterChanged;

        class NotFoundInterpreter : IPythonInterpreter {
            public void Initialize(PythonAnalyzer state) { }
            public IPythonType GetBuiltinType(BuiltinTypeId id) { throw new KeyNotFoundException(); }
            public IList<string> GetModuleNames() { return new string[0]; }
            public event EventHandler ModuleNamesChanged { add { } remove { } }
            public IPythonModule ImportModule(string name) { return null; }
            public IModuleContext CreateModuleContext() { return null; }
        }

        internal class NotFoundInterpreterFactory : IPythonInterpreterFactory {
            public NotFoundInterpreterFactory(
                Guid id,
                Version version,
                string description = null,
                string prefixPath = null
            ) {
                Id = id;
                Configuration = new InterpreterConfiguration(
                    prefixPath,
                    null,
                    null,
                    null,
                    null,
                    ProcessorArchitecture.None,
                    version
                );
                Description = string.IsNullOrEmpty(description) ? string.Format("Unknown Python {0}", version) : description;
            }

            public string Description { get; private set; }
            public InterpreterConfiguration Configuration { get; private set; }
            public Guid Id { get; private set; }

            public IPythonInterpreter CreateInterpreter() {
                return new NotFoundInterpreter();
            }
        }


        class FactoryInfo {
            public readonly MSBuild.ProjectItem ProjectItem;
            public readonly bool Owned;

            public FactoryInfo(MSBuild.ProjectItem projectItem, bool owned) {
                ProjectItem = projectItem;
                Owned = owned;
            }
        }

        #region IDisposable Members

        public void Dispose() {
            _service.DefaultInterpreterChanged -= GlobalDefaultInterpreterChanged;

            if (_factories != null) {
                foreach (var keyValue in _factories) {
                    if (keyValue.Value.Owned) {
                        IDisposable disp = keyValue.Key as IDisposable;
                        if (disp != null) {
                            disp.Dispose();
                        }
                    }
                }
            }
        }

        #endregion
    }
}
