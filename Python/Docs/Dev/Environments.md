PTVS Environment Detection
==========================
Background
----------
Python Tools for Visual Studio provides automatic detection of well-known Python environments. This saves the user from needing to manually enter details such as the interpreter path, version, and library location, as well as allowing PTVS to customize the behavior of the environment. All environments, both detected and manually configured, are shown in the Python Environments window. See [wiki:"the environments guide" Python Environments] for details on how this feature is exposed to users.

Core Interfaces
---------------

There are four primary interfaces involved in environment detection. Other relevant classes and interfaces are described in [Reference](#Reference).

### [src:Microsoft.PythonTools.Interpreter.IPythonInterpreter]

An IPythonInterpreter provides information about modules and types to the analysis engine and IntelliSense. Each installed environment can have multiple IPythonInterpreter instances active at any time. Objects implementing IPythonInterpreter are provided by an IPythonInterpreterFactory.

When using factories based on PythonInterpreterFactoryWithDatabase, there is no need to implement IPythonInterpreter unless the cached type information will be augmented with run-time data.

<a id="ipythoninterpreterfactory" />
### [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterFactory]

Each IPythonInterpreterFactory represents a 'physical' environment, both as represented on disk and as displayed to the user. For example, "Python 2.7" and "Python 3.3" are two separate IPythonInterpreterFactory instances. User projects are always associated with one active factory.

A factory has a stable GUID that uniquely identifies it between sessions: it may be stored in the registry or user project files. Apart from the description, all other settings are provided through an InterpreterConfiguration object.

Generic factories can be created using the `InterpreterFactoryCreator` class or by deriving from PythonInterpreterFactoryWithDatabase. Where the behavior varies significantly from the standard behavior, either IPythonInterpreterFactory or IPythonInterpreterFactoryWithDatabase can be implemented on their own object.

<a id="ipythoninterpreterfactorywithdatabase" />
### [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterFactoryWithDatabase]

Since IPythonInterpreter must provide type information on demand, it is often desirable to pre-generate this information. To allow PTVS to communicate this process to the user, the IPythonInterpreterFactoryWithDatabase interface can be implemented on the same object as IPythonInterpreterFactory. Its purpose is to indicate whether the type database is up to date, and to expose a trigger to begin regenerating the database. See [AnalyzerStatusUpdater](#analyzerstatusupdater) for information on providing status updates while regenerating.

IPythonInterpreterFactoryWithDatabase is optional but strongly recommended when an environment may need time to refresh its cached information. Factories derived from PythonInterpreterFactoryWithDatabase already include a full implementation of this interface using the default database format.

<a id="ipythoninterpreterfactoryprovider" />
### [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterFactoryProvider]

IPythonInterpreterFactoryProvider is implemented by a MEF component. It is a singleton in each process and is responsible for detecting and providing a list of objects implementing IPythonInterpreterFactory. This list may be updated while running if a new environment becomes available, but removals may be ignored (which further implies that the same object must always represent a factory - replacing a factory instance may not behave correctly).

Assemblies containing factory providers are loaded at runtime from local machine and per-user registry keys. See [Registration](#Registration) for details on creating these keys.

Creating a new factory provider
-------------------------------

### CPython-based

Environments based on CPython typically require only [IPythonInterpreterFactoryProvider](#ipythoninterpreterfactoryprovider) to be implemented, with generic factories created using InterpreterFactoryCreator. This allows the default implementation to handle all type information and caching, while providing more customization than a user-configured environment.

Calls to `GetInterpreterFactories` should return all the factories that are available for use. Factories that are not currently installed or are otherwise unavailable should not be returned. Further, each call should return the exact objects that were returned on the previous call, as well as any newly available factories (detected using file system or registry watchers, for example).

Consumers of the provider may call `GetInterpreterFactories` at any time. To ensure that all consumers receive notification of newly available factories, the `InterpreterFactoriesChanged` event should be raised. Currently, factories cannot be removed after they have been detected.

A basic example implementation is shown below. Note that this example will only detect a single factory and only when the provider is first created. (See [src:Microsoft.PythonTools.Interpreter.CPythonInterpreterFactoryProvider] for a complete and more generalized implementation.)

```csharp
[Export(typeof(IPythonInterpreterFactoryProvider))]
[PartCreationPolicy(CreationPolicy.Shared)]
class MyInterpreterFactoryProvider : IPythonInterpreterFactoryProvider {
    private const string MyGuid = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
    private IPythonInterpreterFactory _myFactory;

    public MyInterpreterFactoryProvider() {
        string basePath = @"C:\Python27";
        if (Directory.Exists(basePath)) {
            _myFactory = InterpreterFactoryCreator.CreateInterpreterFactory(
                new InterpreterFactoryCreationOptions {
                    Description = "My Python 2.7",
                    IdString = MyGuid,
                    LanguageVersion = new Version(2, 7),
                    InterpreterPath =       Path.Combine(basePath, "python.exe"),
                    WindowInterpreterPath = Path.Combine(basePath, "pythonw.exe"),
                    LibraryPath =           Path.Combine(basePath, "Lib"),
                    PathEnvironmentVariableName = "PYTHONPATH",
                    Architecture = ProcessorArchitecture.X86,
                    WatchLibraryForNewModules = true
                }
            );
        }
    }

    public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
        if (_myFactory != null) {
            yield return _myFactory;
        }
    }

    public event EventHandler InterpreterFactoriesChanged;
}
```

### Layered (virtualenv-like)

Environments that use multiple sources for libraries are common, and cannot be user-configured. If each library is comprised of Python source files and are not modified often, the easiest implementation is to use [PythonInterpreterFactoryWithDatabase](#pythoninterpreterfactorywithdatabase) to layer one factory on top of another.

(See [src:CanopyInterpreter.CanopyInterpreterFactory] and [src:CanopyInterpreter.CanopyInterpreterFactoryProvider] for a full implementation of a layered factory.)

Layering factories assigns responsibility for maintaining the cached analysis database to each level independently, with each factory being responsible for ensuring the one below it is updated when necessary. Only factories returned by the provider will be visible to the user, allowing the provider to prevent users from directly using any lower-level factories.

The lowest factory can be an instance of `PythonInterpreterFactoryWithDatabase` with the default implementation. Higher levels need to override at least the following methods:

* `IsCurrent`
* `RefreshIsCurrent`
* `MakeTypeDatabase`
* `GenerateDatabase`
* `GetIsCurrentReason`
* `GetFriendlyIsCurrentReason`

`IsCurrent` and `RefreshIsCurrent` need to delegate both to their default implementations and the lower level factory:

```csharp
public override bool IsCurrent {
    get { return !_baseHasRefreshed && _base.IsCurrent && base.IsCurrent; }
}

public override void RefreshIsCurrent() {
    _base.RefreshIsCurrent();
    base.RefreshIsCurrent();
}
```

`MakeTypeDatabase` needs to construct a new `PythonTypeDatabase` passing the lower level's database as the third parameter `innerDatabase`:

```csharp
public override PythonTypeDatabase MakeTypeDatabase(string databasePath, bool includeSitePackages = true) {
    if (_baseDb == null && _base.IsCurrent) {
        _baseDb = _base.GetCurrentDatabase(ShouldIncludeGlobalSitePackages);
    }

    var paths = new List<string> { databasePath };
    if (includeSitePackages) {
        try {
            // Gets directories in the database, not in the library itself.
            paths.AddRange(Directory.EnumerateDirectories(databasePath));
        } catch (ArgumentException) {
        } catch (IOException) {
        } catch (SecurityException) {
        } catch (UnauthorizedAccessException) {
        }
    }
    return new PythonTypeDatabase(this, paths, _baseDb);
}
```

`GenerateDatabase` should regenerate the lower level's database if necessary:

```csharp
public override void GenerateDatabase(GenerateDatabaseOptions options, Action<int> onExit = null) {
    if (!Directory.Exists(Configuration.LibraryPath)) {
        return;
    }

    var req = new PythonTypeDatabaseCreationRequest {
        Factory = this,
        OutputPath = DatabasePath,
        SkipUnchanged = options.HasFlag(GenerateDatabaseOptions.SkipUnchanged)
    };

    req.ExtraInputDatabases.Add(_base.DatabasePath);

    _baseHasRefreshed = false;

    if (_base.IsCurrent) {
        base.GenerateDatabase(req, onExit);
    } else {
        // Specifying our base interpreter as 'WaitFor' allows the UI to
        // forward progress and status messages to the user, even though
        // the factory is not visible.
        req.WaitFor = _base;

        // Because the underlying analysis of the standard library has
        // changed, we must reanalyze the entire database.
        req.SkipUnchanged = false;

        // Clear out the existing base database, since we're going to
        // need to reload it again. This also means that when
        // NewDatabaseAvailable is raised, we are expecting it and won't
        // incorrectly set _baseHasRefreshed to true again.
        _baseDb = null;

        // Start our analyzer first, since we will wait up to a minute
        // for the base analyzer to start (which may cause a one minute
        // delay if it completes before we start, but that is unlikely).
        base.GenerateDatabase(req, onExit);
        _base.GenerateDatabase(GenerateDatabaseOptions.SkipUnchanged);
    }
}
```

Finally, `GetIsCurrentReason` and `GetFriendlyIsCurrentReason` should provide information from the lower level factory to ensure users are aware of why they need to refresh the database. The messages can be changed if the lowel level factory is not visible to the user:

```csharp
public override string GetFriendlyIsCurrentReason(IFormatProvider culture) {
    if (_baseHasRefreshed) {
        return "Base interpreter has been refreshed";
    } else if (!_base.IsCurrent) {
        return string.Format(culture,
            "Base interpreter is out of date:{0}{0}    {1}",
            Environment.NewLine,
            _base.GetFriendlyIsCurrentReason(culture));
    }
    return base.GetFriendlyIsCurrentReason(culture);
}

public override string GetIsCurrentReason(IFormatProvider culture) {
    if (_baseHasRefreshed) {
        return "Base interpreter has been refreshed";
    } else if (!_base.IsCurrent) {
        return string.Format(culture,
            "{0} is out of date:{1}{2}",
            _base.Description,
            Environment.NewLine,
            _base.GetIsCurrentReason(culture));
    }
    return base.GetIsCurrentReason(culture);
}
```


### Other
Environments that are unable or unwilling to use the default implementations of the type database must implement the core interfaces without reference to any other PTVS classes. Implementations of the following interfaces will be required:

* [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterFactoryProvider]
 * Provides instances of objects implementing `IPythonInterpreterFactory`.
* [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterFactory]
 * Provides instances of objects implementing `IPythonInterpreter`.
* [src:Microsoft.PythonTools.Interpreter.IPythonInterpreter]
 * Provides access to Python types provided by an environment.
* [src:Microsoft.PythonTools.Interpreter.IPythonModule]
 * Provides information about a module object and its members.
* [src:Microsoft.PythonTools.Interpreter.IPythonType]
 * Provides information about a type object and its members.
* [src:Microsoft.PythonTools.Interpreter.IPythonFunction]
 * Provides information about a function or method and its overloads.
* [src:Microsoft.PythonTools.Interpreter.IPythonFunctionOverload]
 * Provides information about a single overload of a callable object.
* [src:Microsoft.PythonTools.Interpreter.IParameterInfo]
 * Provides information about a single parameter of an overload.

The following interfaces may be implemented to better interact with PTVS.

* [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterFactoryWithDatabase]
 * Provides user feedback and interaction with long-running type cache updates.
* [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterWithProjectReferences]
 * Allows individual modules to be added as references.
* [src:Microsoft.PythonTools.Interpreter.ILocatedMember]
 * Provides location information for any object that also implements `IMember`.
* [src:Microsoft.PythonTools.Interpreter.IModuleContext]
 * Associates opaque interpreter state with modules, allowing instances to be shared.
* [src:Microsoft.PythonTools.Interpreter.IAdvancedPythonType]
 * An extended version of `IPythonType` with support for non-standard Python type.
* [src:Microsoft.PythonTools.Interpreter.IBuiltinProperty]
 * Provides information about `property` objects.
* [src:Microsoft.PythonTools.Interpreter.IPythonConstant]
 * Represents a constant of a specific type. The value of the constant is not necessary.
* [src:Microsoft.PythonTools.Interpreter.IPythonEvent]
 * Provides information about events.
* [src:Microsoft.PythonTools.Interpreter.IPythonMethodDescriptor]
 * Provides information about a bound function.
* [src:Microsoft.PythonTools.Interpreter.IPythonMultipleMembers]
 * Provides information about multiple values appearing under a single name.
* [src:Microsoft.PythonTools.Interpreter.IPythonSequenceType]
 * Provides information about the contents of a collection.

Registration
------------

Interpreter factory providers are exposed using MEF (see [IPythonInterpreterFactoryProvider](#ipythoninterpreterfactoryprovider)). Assemblies containing providers are registered in the `InterpreterFactories` registry key. This key is available in three locations:

```
HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\$(VisualStudioVersion)_Config\PythonTools\InterpreterFactories
HKEY_CURRENT_USER\Software\Microsoft\PythonTools\$(VisualStudioVersion)\InterpreterFactories
HKEY_LOCAL_MACHINE\Software\Microsoft\PythonTools\$(VisualStudioVersion)\InterpreterFactories
```

(Note that these are 32-bit registry entries; 64-bit processes should request a 32-bit view of the registry in order to correctly access the local machine key.)

`$(VisualStudioVersion)` should substituted for each of `12.0`, `11.0` and `10.0`. Separate registration for each version of Visual Studio is required, and because of a required reference to `Microsoft.PythonTools.Analysis.dll`, separate binaries are required for each targeted version. (These can all be built from the same source code, provided they target the correct version of `Microsoft.PythonTools.Analysis.dll`.)

Subkeys of `InterpreterFactories` are created when registering a provider. The subkey name should be unique to avoid conflicting with other providers (using a GUID is recommended), but it has no meaning. Each subkey has a string value named `CodeBase` that specifies the full path to the assembly containing the provider.

For example, these are the keys that are registered as part of a normal installation for VS 2013:

```
HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\12.0_Config\PythonTools\InterpreterFactories\
    {2AF0F10D-7135-4994-9156-5D01C9C11B7E}\
        CodeBase="C:\Program Files\...\Microsoft.PythonTools.VSInterpreters.DLL"
    {80659AB7-4D53-4E0C-8588-A766116CBD46}\
        CodeBase="C:\Program Files\...\Microsoft.PythonTools.IronPython.Interpreter.DLL"
    {9A7A9026-48C1-4688-9D5D-E5699D47D074}\
        CodeBase="C:\Program Files\...\Microsoft.PythonTools.VSInterpreters.DLL"
    {FCC291AA-427C-498C-A4D7-4502D6449B8C}\
        CodeBase="C:\Program Files\...\Microsoft.PythonTools.IronPython.Interpreter.DLL"
    ConfigurablePythonInterpreterFactoryProvider\
        CodeBase="C:\Program Files\...\Microsoft.PythonTools.VSInterpreters.DLL"
```

Assemblies specified multiple times are only loaded once, and all exported providers will be used; repeating an entry provides no value, but is permitted. The default value of the key may be used freely for descriptive text, but all other values are reserved for future use.

The key under the Visual Studio configuration hive (`HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\...`) is provided for factories registered as part of a Visual Studio extension. See [Registering VSPackages](http://msdn.microsoft.com/en-us/library/bb165927.aspx) on MSDN for information on adding custom registration information to a generated pkgdef file. *Do not modify this key by hand or from an external installer.*

Installers should use the Python Tools keys for either the local machine (`HKEY_LOCAL_MACHINE\Software\Microsoft\PythonTools\...`) or current user (`HKEY_CURRENT_USER\Software\Microsoft\PythonTools\...`). All providers specified in any of these keys will be loaded - there is no priority order.

### Testing Registration

The following snippet of code can be run with IronPython to determine which providers have been loaded and which interpreters are detected. `GetExportedValue` will raise a detailed error if any provider cannot be loaded.

```python
import clr
clr.AddReference("System.ComponentModel.Composition")
clr.AddReference("Microsoft.PythonTools")
clr.AddReference("Microsoft.PythonTools.VSInterpreters")
from System.ComponentModel.Composition.Hosting import *
from Microsoft.PythonTools.Interpreter import *
container = CompositionContainer(AssemblyCatalog(clr.GetClrType(IInterpreterOptionsService).Assembly))

# This line will raise a detailed error if the provider cannot be loaded.
service = container.GetExportedValue[IInterpreterOptionsService]()

providers = list(service.KnownProviders)
factories = list(service.Interpreters)
print [type(provider).__name__ for provider in providers]
print [factory.Description for factory in factories]
```

Full testing should be conducted inside Visual Studio.

Reference
---------

<a id="analyzerstatusupdater" />
### [src:Microsoft.PythonTools.Analysis.Analyzer.AnalyzerStatusUpdater]

Provides an active object that communicates with all running PTVS sessions on the local machine. Communication is limited to providing progress updates, which will be displayed in the Python Environments window for the associated interpreter factory.

Because each instance of this object creates a thread and holds references to shared memory and synchronization objects, it is important to call Dispose() when no longer in use.

Factories based on [PythonInterpreterFactoryWithDatabase](#pythoninterpreterfactorywithdatabase) make use of this class with the external analyzer process. There is no need to use this class unless you are implementing a new analyzer.

`TODO: Usage example`

### [src:Microsoft.PythonTools.Interpreter.InterpreterConfiguration]

Specifies all the configurable properties for an interpreter factory. Importantly, it specifies the Python language version and paths to interpreter executables. 

See [InterpreterFactoryCreator](#interpreterfactorycreator) for example usage.

<a id="interpreterfactorycreator" />
### [src:Microsoft.PythonTools.Interpreter.InterpreterFactoryCreator]

A static class that provides helper functions for creating generic interpreter factories based on PythonInterpreterFactoryWithDatabase. The creation options are provided as an instance of [src:Microsoft.PythonTools.Interpreter.InterpreterFactoryCreationOptions].

```csharp
var factory = InterpreterFactoryCreator.CreateInterpreterFactory(
    new InterpreterFactoryCreationOptions {
        Description = "My Python 2.7",
        IdString = MyGuid,
        LanguageVersion = new Version(2, 7),
        InterpreterPath = @"C:\Python27\python.exe",
        WindowInterpreterPath = @"C:\Python27\pythonw.exe",
        LibraryPath = @"C:\Python27\Lib",
        PathEnvironmentVariableName = "PYTHONPATH",
        Architecture = ProcessorArchitecture.X86,
        WatchLibraryForNewModules = true
    }
);
```

### [src:Microsoft.PythonTools.Interpreter.IPythonInterpreterWithProjectReferences]

Implemented on the same object implementing IPythonInterpreter to support references to other VS projects, such as C++ projects that compile to .pyd.

See the implementation in [src:Microsoft.PythonTools.Interpreter.CPythonInterpreter] for usage.

<a id="pythoninterpreterfactorywithdatabase" />
### [src:Microsoft.PythonTools.Interpreter.PythonInterpreterFactoryWithDatabase]

An implementation of [IPythonInterpreterFactoryWithDatabase](#ipythoninterpreterfactorywithdatabase) that uses the default database format ([PythonTypeDatabase](#pythontypedatabase)) and assumes that the environment is laid out similarly to a standard CPython install. This applies for most distributions of CPython and some other interpreters, and so is often a much simpler way to implement a new factory.

See [InterpreterFactoryCreator](#interpreterfactorycreator) for methods to create instances of this class, rather than deriving from it. Environments that want to use a different database format should implement the core interpreter interfaces and avoid the `PythonInterpreterFactoryWithDatabase` class.

<a id="pythontypedatabase" />
### [src:Microsoft.PythonTools.Interpreter.PythonTypeDatabase]

The default database implementation used for storing cached type information. Using PythonTypeDatabase allows extensions to avoid having to reimplement most of the interpreter interfaces and analysis generation, but requires the interpreter to be sufficiently similar to CPython that the scraper scripts work ([file:PythonScraper.py], [file:BuiltinScraper.py] and [file:ExtensionScraper.py]) and the standard library is available as Python source files.

Environments that want to use a different database format should implement the core interpreter interfaces and avoid the `PythonInterpreterFactoryWithDatabase` class.

`TODO: Usage example`
