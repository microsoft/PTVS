# Changelog

## 2020.9.4 (10 September 2020)

Notable changes:

-   Bug Fix: Addressing memory and cpu issues a number of users had by no longer indexing libraries and unopened files at startup. This will revert auto-import completions and workspace symbols performance to previous levels.
    ([pylance-release#321](https://github.com/microsoft/pylance-release/issues/321))

In addition, Pylance's copy of Pyright has been updated from 1.1.66 to 1.1.70, including the following changes:

-   [1.1.70](https://github.com/microsoft/pyright/releases/tag/1.1.70)
    -   Enhancement: Added support for PEP 585. Standard collection types defined in builtins can now be used like their typing counterparts. This includes "tuple", which needs special-case handling because its class definition in builtins.pyi indicates that it has a single type parameter, but it actually supports variadic parameters.
    -   Bug Fix: Added code to prevent heap overrun errors during parsing/binding, most notably during indexing operations.
    -   Bug Fix: Fixed bug that caused runtime crash if typeshed stubs couldn't be found or didn't define 'tuple'.
    -   Bug Fix: Improved interaction between recursive type aliases and bidirectional type inference for lists and dicts.
    -   Bug Fix: Improved type narrowing for assignments in cases where the destination of the assignment is declared as a union and the assigned type is a narrower form of one of the union elements. Previously, the narrowing logic didn't choose the narrowest type possible in this case.
    -   Enhancement: Added perf optimization for unions that contain hundreds or thousands of int literal values. This is similar to another recent optimization for str literal unions.
    -   From Pylance: Ensure that auto-import doesn't place import statement below usage.
-   [1.1.69](https://github.com/microsoft/pyright/releases/tag/1.1.69)
    -   Enhancement: Improved type analysis perf by about 5% and reduced memory usage slightly by not formatting and logging diagnostic messages in cases where they are suppressed (e.g. argument type mismatches when doing overload matching).
    -   Bug Fix: Fixed bug that affected dependency tracking of source files on platforms with case-insensitive file systems. In some cases, the case of paths differed, and the logic was treating these as separate files.
    -   Enhancement: Added diagnostics for type variables that are used improperly as defined in PEP 484: 1) conflicting type variables that are used in nested generic class declarations, and 2) type variables that are used within annotations outside of a context in which they have meaning.
    -   New Feature: Added support for "higher-order" type variables. You can now pass a generic function as an argument to another generic function, and the type var solver can solve the type variables for both at the same time.
    -   New Feature: Added support for recursive type aliases.
    -   Behavior Change: Updated the default Python version from 3.8 to 3.9. This is used only if it is not otherwise configured and there is no Python environment from which to determine the version.
    -   Enhancement: Added checks for usage of certain built-in types that are defined as generic in the typeshed stubs but generate runtime exceptions if subscripted on older versions of Python (prior to 3.9). Such types need to be enclosed in quotes when used in annotations.
-   [1.1.67](https://github.com/microsoft/pyright/releases/tag/1.1.67)
    -   Bug Fix: Fixed bug that caused the recently-added "discriminated field type narrowing" to be used in cases where it should not. This resulted in types being narrowed inappropriately when a field was typed as a union of literals.
    -   Behavior Change: Changed command-line version to not print any non-JSON output when "--outputjson" option is used.
    -   Behavior Change: Changed behavior when "useLibraryCodeForTypes" is set to "false". Previously, all ".py" library code was ignored in this case. Now, ".py" types are used for types if the package has an associated "py.typed" file as specified in PEP 561. Packages with no "py.typed" file will still be ignored if "useLibraryCodeForTypes" is "false".
    -   Bug Fix: Fixed a couple of bugs that resulted in the hover text incorrectly identifying a symbol as a "type alias".
    -   Behavior Change: Changed type inference logic to use "List", "Set", and "Dict" rather than "list", "set" and "dict" when inferring the type of a list, set or dict expression. These are aliases for the same underlying class, but the upper-case versions are more consistent with type annotations used within the code.
    -   Bug Fix: Fixed "NoReturn" inference logic for async functions. This logic was previously flagging the code after a call to such a function as unreachable.
    -   Enhancement: Improved parser to detect syntax errors involving unpack operator within a comprehension.
    -   Enhancement: Changed import resolution logic to allow binaries (e.g. ".so" files) to satisfy local imports (within the package), not just third-party imports (within site-packages).
    -   Enhancement: Extended bidirectional type inference (expected types) to list comprehensions.
    -   New Feature: Added new diagnostic rule "reportPropertyTypeMismatch" that verifies that the type of the input parameter to a property's setter is assignable to the return type of the getter.
    -   Bug Fix: Fixed bug that caused a crash in the type checker in cases where type arguments were not provided to a few special-case built-in classes.
    -   Bug Fix: Fixed a bug in the handling of generics that involve constrained TypeVars. The TypeVar matching logic was sometimes inappropriately specializing the type using the first constrained type.
    -   Bug Fix: Added special-case handling in type checker for callers who request the type of an expression that happens to be a name used in a call expression to designate a named parameter. This isn't really an expression, so the code wasn't handling it correctly, but callers (such as the hover provider and the new semantic token provider) were assuming that it was safe. This resulted in incorrect "X is not defined" diagnostics being logged.

## 2020.9.0 (3 September 2020)

Notable changes:

-   Pylance now supports semantic highlighting. In order to enable this feature, you must be using at least version 2020.8.106424 of the Python extension, as well as a VS Code theme which includes semantic colorization support (e.g., Dark+, Light+, One Dark Pro, others).
    ([pylance-release#220](https://github.com/microsoft/pylance-release/issues/220))
-   Pylance will now index libraries and unopened files at startup to provide auto-import completions even for variables that have not been fully analyzed. This index is also used to improve the performance of the workspace symbols search.
-   The auto-import completions offered should now more accurately reflect the "intended" import, rather than suggesting importing deeper modules. This helps improve the behavior in libraries that re-export symbols through other modules.
    ([pylance-release#222](https://github.com/microsoft/pylance-release/issues/222), [pylance-release#139](https://github.com/microsoft/pylance-release/issues/139), [pylance-release#28](https://github.com/microsoft/pylance-release/issues/28), [pylance-release#97](https://github.com/microsoft/pylance-release/issues/97))
-   The auto-import completion tooltip now more clearly states what will be added to your import block. For example, a completion for "join" will explicitly say `from os.path import join`, rather than just "Auto-import from os.path".
-   When the `completeFunctionParens` feature is enabled, the signature help will now open automatically, matching the behavior when the parentheses are user-written.
    ([pylance-release#273](https://github.com/microsoft/pylance-release/issues/273))
-   Pylance now includes schemas for `pyrightconfig.json`/`mspythonconfig.json`, which enables code completion and validation for these config files.
    ([pylance-release#40](https://github.com/microsoft/pylance-release/issues/40))
-   Methods which only raise `NotImplementedError` will now be treated as abstract and not be marked as not returning, preventing some child class functions from being spuriously marked as dead code. Explicitly declaring classes and methods as abstract is still strongly preferred as it allows the type checker to more accurately check child classes for correctness.
    ([pylance-release#248](https://github.com/microsoft/pylance-release/issues/248))
-   The default `stubPath` now correctly shows in the VS Code settings UI with its default "typings".
    ([pylance-release#285](https://github.com/microsoft/pylance-release/issues/285))

In addition, Pylance's copy of Pyright has been updated from 1.1.65 to 1.1.66, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Bug Fix: Fixed bug that caused the recently-added "discriminated field type narrowing" to be used in cases where it should not. This resulted in types being narrowed inappropriately when a field was typed as a union of literals.
-   [1.1.66](https://github.com/microsoft/pyright/releases/tag/1.1.66)
    -   Enhancement: Improved completion suggestion behavior when the insertion point is between an identifier and an empty index (e.g. "f[]") or in the presence of a missing right square bracket (e.g. "f.[").
    -   Behavior Change: Changed diagnostic related to type argument count to be controlled by the "reportGeneralTypeIssues" diagnostic rule. It was previously always emitted as an error.
    -   From Pylance: Fix progress reporter type, auto-import/symbol changes, worker thread updates, improve auto-import tooltips (#977)
    -   Enhancement: Updated typeshed stubs to the latest version.
        ([pylance-release#293](https://github.com/microsoft/pylance-release/issues/293))
    -   Bug Fix: Eliminated incorrect error when "super()" was used in a class where one or more parent classes were of an unknown type.
    -   Bug Fix: Changed the handling of old-style comment method annotations to accept an optional annotation for "self" and "cls" parameters.
    -   Bug Fix: Changed handling of dataclass classes that derive from a class whose type is unknown. The synthesized constructor now allows any parameter list in this case.
    -   Enhancement: Improved completion provider to distinguish properties from other methods.
        ([pylance-release#299](https://github.com/microsoft/pylance-release/issues/299))
    -   Behavior Change: Changed heuristics for function return type inference so methods that raise a NotImplementedError and have no other return path have an inferred return type of Unknown rather than NoReturn. Such methods should be marked as abstract, but frequently they are not.
    -   Behavior Change: Changed the behavior of the import resolution logic to fail an import resolution of a multi-part name (e.g. "a.b.c") if it can't be fully resolved. This could produce false positives in cases where third-party libraries are using dynamic tricks to manipulate their package namespace, but it will eliminate false negatives.
    -   Bug Fix: Suppress the use of "Unnecessary" diagnostic hints (used to display variables and code blocks in gray) if the LSP client claims not to support this tag.
    -   Enhancement: Added new "reportMissingTypeArgument" diagnostic rule and enabled it by default in "strict" mode. It generates a diagnostic when a generic class or generic type alias is used in an annotation with no type arguments provided.
    -   Bug Fix: Fixed handling of scopes for nested classes. The previous logic allowed an inner class to access variables defined in an outer class, which is not permitted.
    -   Enhancement: Added check for raise statements that take an exception class but the class constructor requires one or more arguments.
    -   Bug Fix: Fixed bug in tokenizer that cause line numbers to be off when an invalid token occurred at the end of a line.
    -   Bug Fix: Fixed a bug in the Pyright parser. It was not correctly following the Python grammar spec when parsing type annotations, so it generated syntax errors in some cases where that was inappropriate.
    -   Enhancement: Added a check and a general type diagnostic for metaclass conflicts.

## 2020.8.3 (28 August 2020)

Notable changes:

-   Overall memory usage has been improved; in many use cases, peak memory usage has been reduced by 10%.
-   Performance with large unions of `Literal` strings has been greatly improved.
-   Type aliases now show more consistently in tooltips.
-   The upcoming Python 3.10 `typing.TypeAlias` (PEP 613) is now supported.

In addition, Pylance's copy of Pyright has been updated from 1.1.64 to 1.1.65, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Bug Fix: Improved completion suggestion behavior when the insertion point is between an identifier and an empty index (e.g. "f[]") or in the presence of a missing right square bracket (e.g. "f.[")
        ([pylance-release#286](https://github.com/microsoft/pylance-release/issues/286))
    -   Behavior Change: Changed diagnostic related to type argument count to be controlled by the "reportGeneralTypeIssues" diagnostic rule. It was previously always emitted as an error.
        ([pylance-release#290](https://github.com/microsoft/pylance-release/issues/290))
-   [1.1.65](https://github.com/microsoft/pyright/releases/tag/1.1.65)
    -   Bug Fix: Fixed bug in command-line version that caused an error to be reported when "useLibraryCodeForTypes" or "verboseOutput" was specified in the pyrightconfig.json file.
    -   Enhancement: Added support for protocol matching where the protocol includes an overloaded method.
    -   Enhancement: Improved diagnostic messages for function type mismatches.
    -   Enhancement: Improved diagnostic messages for tuple matching and union assignments.
    -   Enhancement: Changed nested diagnostic messages to use non-breaking spaces so indentations are visible within the VS Code "Problems" panel.
    -   Bug Fix: Fixed bug in reportIncompatibleMethodOverride diagnostic check. The logic was checking for wider parameter types when it should have been checking for narrower.
    -   Bug Fix: Fixed bug in method override validation code. It wasn't applying partial specialization of the base class, resulting in inappropriate errors in some cases.
    -   Bug Fix: Fixed bug in the type evaluation of expressions with + or - operators and integer literal operands. These expressions should evaluate to a literal type, not an int.
        ([pylance-release#260](https://github.com/microsoft/pylance-release/issues/260))
    -   Bug Fix: Fixed bug in parsing of f-strings that contain \N escape and a Unicode character name that includes a hyphen.
        ([pylance-release#263](https://github.com/microsoft/pylance-release/issues/263))
    -   Bug Fix: Fixed bug in type evaluator that caused an incorrect error when a class decorator was used for a generic class.
    -   Bug Fix: (From Pylance) Fixed performance problem related to file change events triggered by reads from site-packages.
    -   Enhancement: Enabled support for PEP 613 (TypeAlias).
    -   Bug Fix: Fixed bug that caused type aliases to get expanded in some contexts when they shouldn't.
        ([pylance-release#265](https://github.com/microsoft/pylance-release/issues/265))
    -   Bug Fix: Fixed bug that caused "from .A import \*" to work incorrectly when the wildcard included symbol A.
        ([pylance-release#269](https://github.com/microsoft/pylance-release/issues/269))
    -   Enhancement: Added logic in completion provider to return class variables in base classes when the insertion point is in the context of a subclass body.
    -   Bug Fix: Fixed TypeAlias code to check for Python 3.10 rather than 3.9 since PEP 613 has been moved out to 3.10.
    -   Enhancement: Added performance optimization for TypedDict classes. Entries are now computed once and cached in the class type. This provides a big speed-up for TypeDict classes that have a large number of fields.
    -   Enhancement: Added performance optimization for union types that contain large numbers of string literals. The code for inserting new items into a union is O(n^2); this optimization makes it O(n) for string literal types.
    -   Bug Fix: Fixed bug that caused custom import aliases of "Final", "Literal" and "TypeAlias" to not work correctly.
    -   Bug Fix: Fixed bug that resulted in spurious errors when hovering over module names in import statements.
    -   Bug Fix: Fixed several bugs relating to symbols introduced into a class by its metaclass.
        ([pylance-release#154](https://github.com/microsoft/pylance-release/issues/154))
    -   Bug Fix: Fixed bug that caused type analyzer to crash when a nonlocal binding referred to a symbol that was not present in an outer scope and then was assigned to.

## 2020.8.2 (20 August 2020)

Notable changes:

-   The new `python.analysis.completeFunctionParens` option adds parenthesis to function and method completions. This option is disabled by default.
    ([pylance-release#37](https://github.com/microsoft/pylance-release/issues/37))
-   Workspace symbol searching will no longer search or return results from libraries or bundled type stubs, which greatly improves its performance.
    ([pylance-release#34](https://github.com/microsoft/pylance-release/issues/34), [pylance-release#228](https://github.com/microsoft/pylance-release/issues/228))
-   File watching support has been improved, leading to improved performance and lower peak memory consumption.
-   Settings from MPLS (for example `python.autoComplete.extraPaths` and `python.autoComplete.addBrackets`) will now be automatically ported to their updated names if present and Pylance is enabled.

In addition, Pylance's copy of Pyright has been updated from 1.1.62 to 1.1.64, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Bug Fix: Fixed bug in type evaluator that caused an incorrect error when a class decorator was used for a generic class.
    -   Bug Fix: Fixed bug in parsing of f-strings that contain \N escape and a Unicode character name that includes a hyphen.
        ([pylance-release#263](https://github.com/microsoft/pylance-release/issues/263))
    -   Behavior Change: Changed capitalization of the python.analysis.logLevel setting so it matches Pylance. The settings code in Pyright is case insensitive, but the JSON editor emits a warning if the case doesn't match.
    -   Bug Fix: Fixed bug in the type evaluation of expressions with + or - operators and integer literal operands. These expressions should evaluate to a literal type, not an int.
        ([pylance-release#260](https://github.com/microsoft/pylance-release/issues/260))
    -   Bug Fix: Fixed bug in method override validation code. It wasn't applying partial specialization of the base class, resulting in inappropriate errors in some cases.
    -   Bug Fix: Fixed bug in reportIncompatibleMethodOverride diagnostic check. The logic was checking for wider parameter types when it should have been checking for narrower.
    -   Enhancement: Changed nested diagnostic messages to use non-breaking spaces so indentations are visible within the VS Code "Problems" panel.
    -   Enhancement: Improved diagnostic messages for tuple matching and union assignments.
    -   Enhancement: Added support for protocol matching where the protocol includes an overloaded method.
    -   Bug Fix: Fixed bug in command-line version that caused an error to be reported when "useLibraryCodeForTypes" or "verboseOutput" was specified in the pyrightconfig.json file.
-   [1.1.64](https://github.com/microsoft/pyright/releases/tag/1.1.64)
    -   Bug Fix: Fixed regression that caused "isinstance(x, Callable)" to be flagged as an error when PEP 484 says that it's legal.
        ([pylance-release#247](https://github.com/microsoft/pylance-release/issues/247))
    -   Enhancement: Changed error messages related to "partially unknown" types to expand type aliases, which can obscure the unknown part of the type.
    -   Enhancement: Added support for narrowing types based on the pattern `A.B == <literal>` and `A.B != <literal>` when A has a union type and all members of the union have a field "B" with a declared literal type that discriminates one sub-type from another.
    -   Enhancement: Added bidirectional type inference for ternary expressions.
    -   Bug Fix: Fixed incorrect handling of member accesses when the accessed field had a type that was a union between two or more classes, some with special accessor methods (e.g. `__get__`) and some without.
    -   Enhancement: Improved type checking for assignments of callable types. Previously, certain edge cases were ignored.
    -   Enhancement: Added code to check for overlapping (obscured) overload functions.
    -   Bug Fix: Fixed bug that caused incorrect evaluation of type alias that refers to Literal types. The literal values were being stripped in some cases.
    -   Bug Fix: Fixed recent regression that caused type aliases that described literal types to be printed incorrectly in hover text and error messages.
    -   Enhancement: Added code to report overloads that overlap in an "unsafe" way — i.e. they can potentially accept the same arguments but return different (incompatible) types.
    -   Enhancement: Updated typeshed stubs to latest version.
    -   Bug Fix: Fixed bug in assignment checks between homogeneous multi-length tuples and fixed-size tuples.
-   [1.1.63](https://github.com/microsoft/pyright/releases/tag/1.1.63)
    -   Enhancement: Diagnostic rule severity overrides are now editable in the VS Code settings UI.
    -   Bug Fix: Fixed out-of-memory error that occurred during a workspace "find symbols" operation. We were not properly checking for the heap high watermark during this operation.
        ([pylance-release#254](https://github.com/microsoft/pylance-release/issues/254))
    -   Enhancement: Added support for special type "Counter" exported by typing module, which is an alias for collections.Counter.
    -   Bug Fix: Fixed bug in bidirectional type inference for dictionary statements. The logic was not allowing for dict subclass Mapping.
    -   Enhancement: Improved type checker's handling of "in" operator. It previously flagged an error if the right operand didn't support a `__contains__` method. It now properly checks for iterable types as well.
    -   Bug Fix: Fixed bug that caused incorrect evaluation of symbol types within a chain of assignments (e.g. "a = b = c = 4") in some cases.
    -   Enhancement: Enabled file watcher for libraries to detect changes in installed packages. This behavior is already standard for Pylance, but it was disabled for Pyright.
    -   Enhancement: Improved handling of Tuple type. The type checker now does a better job retaining the types of the individual elements within a Tuple or a class that derives from a Tuple.
    -   Enhancement: Improved support for NamedTuple classes and classed derived from NamedTuple. The type checker now retains types of individual elements when used with unpacking and indexing operators.
        ([pylance-release#251](https://github.com/microsoft/pylance-release/issues/251))
    -   Behavior Change: Changed "find workspace symbols" to return only symbols from within user code or opened files, not library files that are closed.
        ([pylance-release#34](https://github.com/microsoft/pylance-release/issues/34), [pylance-release#228](https://github.com/microsoft/pylance-release/issues/228))
    -   Bug Fix: Fixed recent regression that caused incorrect errors to be generated in sub files for certain call expressions.
        ([pylance-release#243](https://github.com/microsoft/pylance-release/issues/243))
    -   New Feature: Added support for Concatenate as described in latest version of PEP 612. Added ParamSpec and Concatenate to typing.pyi.

## 2020.8.1 (13 August 2020)

Notable changes:

-   The `pandas` stubs have been further improved.
    ([pylance-release#27](https://github.com/microsoft/pylance-release/issues/27), [pylance-release#90](https://github.com/microsoft/pylance-release/issues/90), [pylance-release#144](https://github.com/microsoft/pylance-release/issues/144), [pylance-release#148](https://github.com/microsoft/pylance-release/issues/148), [pylance-release#202](https://github.com/microsoft/pylance-release/issues/202))
-   The VS Code settings editor (both UI and JSON) now provides hints for `python.analysis.diagnosticSeverityOverrides`, listing all valid options, their values, and descriptions.
-   Old-style `# type` comments for function signature type annotations are now supported. This syntax is underspecified and not preferred, but is commonly used to provide compatibility with (the now end-of-life) Python 2, and may improve the usability of some libraries.

In addition, Pylance's copy of Pyright has been updated from 1.1.60 to 1.1.62, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Bug Fix: Fixed bug that caused incorrect evaluation of symbol types within a chain of assignments (e.g. "a = b = c = 4") in some cases.
    -   Enhancement: Improved type checker's handling of "in" operator. It previously flagged an error if the right operand didn't support a `__contains__` method. It now properly checks for iterable types as well.
    -   Bug Fix: Fixed bug in bidirectional type inference for dictionary statements. The logic was not allowing for dict subclass Mapping.
    -   Enhancement: Added support for special type "Counter" exported by typing module, which is an alias for collections.Counter.
        ([pylance-release#229](https://github.com/microsoft/pylance-release/issues/229))
    -   Bug Fix: Fixed out-of-memory error that occurred during a workspace "find symbols" operation. We were not properly checking for the heap high watermark during this operation.
        ([pylance-release#228](https://github.com/microsoft/pylance-release/issues/228))
-   [1.1.62](https://github.com/microsoft/pyright/releases/tag/1.1.62)
    -   Bug Fix: Fixed bug in the handling of unrecognized escape sequences within string literals.
        ([pylance-release#219](https://github.com/microsoft/pylance-release/issues/219))
    -   Bug Fix: Fixed bug related to a subtle interaction between bidirectional type inference of list expressions that contain literal values and TypeVar matching. The previous logic was incorrectly matching T in `List[T]` and the list contained a literal type. It should have stripped the literal if possible.
    -   Enhancement: Added diagnostic message for TypeVar with a single constraint type for consistency with mypy.
    -   Enhancement: Added support for member access completion suggestions when the LHS is a function or a None type.
        ([pylance-release#214](https://github.com/microsoft/pylance-release/issues/214))
    -   Behavior Change: Behavior change for type stub generator: don't emit `__all__` assignments or assignments to self.xxx in function bodies. These violate PEP 484 guidelines for type stubs.
    -   Enhancement: Added diagnostic check to reportInvalidStubStatement that flags parameter default value expressions that are not "..." in stub files.
    -   Bug Fix: Fixed bug that caused annotated types of vargs and kwargs parameters not to be printed in hover text.
    -   Enhancement: Implemented support for older-style function annotation type comments. I previously resisted adding this additional complexity, but we're seeing many libraries that still contain these annotations for backward compatibility with Python 2.
    -   Bug Fix: Fixed bug that caused a crash in the type analyzer when a protocol class referred to itself.
        ([pylance-release#225](https://github.com/microsoft/pylance-release/issues/225))
    -   Enhancement: Added support for "useLibraryCodeForTypes" option in config file. It overrides the client setting of the same name or the "--lib" command-line option.
    -   Bug Fix: Fixed several bugs in logging for config errors.
    -   Enhancement: Added logic to type checker to validate that the "self" or "cls" parameter with a specified type annotation is assignable when binding the method to an object or class.
    -   Enhancement: Improved type assignment diagnostic message. Added "(property)" designator to the end of a property type to differentiate it from a normal attribute.
    -   Enhancement: Added code to validate that method overloads are all abstract or not.
    -   Enhancement: Updated typeshed stubs to the latest.
-   [1.1.61](https://github.com/microsoft/pyright/releases/tag/1.1.61)
    -   Bug Fix: Fixed bug that caused symbols to be marked unaccessed if they were accessed by code that is not accessible (e.g. due to conditional execution based on the platform).
    -   Bug Fix: Updated PEP 604 and PEP 612 error message to refer to Python 3.10 instead of 3.9.
    -   Behavior Change: Changed logic that validates "self" or "cls" parameter names to ignore the check if the provided parameter name begins with an underscore, as is seen in several typeshed stub files.
    -   Bug Fix: Fixed bug in nested f-string parsing when f-string contains triple quotes that include single quotes.
        ([pylance-release#203](https://github.com/microsoft/pylance-release/issues/206))
    -   Bug Fix: Fixed handling of a class that is subclassed from both Enum and another class (like str).
    -   Enhancement: Added support for generic classes that refer to themselves as type arguments within their base class.
    -   Bug Fix: Improved error message for partially-unknown types that have a type alias.
    -   Bug Fix: Allow use of forward-declared classes as subclass in class declarations within type stub files.
    -   Bug Fix: Add special-case handling of `__class_getitem__` method, which acts as a class method even though it is not decorated as such.
    -   Bug Fix: Added missing validation of arguments to `type` call.
    -   Enhancement: Added `=` character to end of named parameter for completion suggestions within a call signature.
        ([pylance-release#209](https://github.com/microsoft/pylance-release/issues/209))
    -   Bug Fix: Added client capability check for signature information "labelOffsetSupport" for compatibility with clients that don't support this capability.
    -   Bug Fix: When adding completion suggestions to the list for expression completion, avoid adding duplicately-named symbols that appear in nested scopes.
        ([pylance-release#215](https://github.com/microsoft/pylance-release/issues/215))
    -   Bug Fix: Fixed bug related to calls of methods on a metaclass via classes that are constructed by that metaclass.
    -   Enhancement: Added check for single @overload function with no additional overloads.

## 2020.8.0 (5 August 2020)

-   Added `python.analysis.autoImportCompletions` setting (`true` by default), which allows auto-import completions to be disabled.
    ([pylance-release#64](https://github.com/microsoft/pylance-release/issues/64))
-   Fixed the "make Pylance your default language server" prompt when language server setting was previously set outside of the user settings.

In addition, Pylance's copy of Pyright has been updated from 1.1.58 to 1.1.60, including the following changes:

-   [1.1.60](https://github.com/microsoft/pyright/releases/tag/1.1.60)
    -   Bug Fix: Fixed a bug "aliased import with leading underscore produces private usage error".
    -   Bug Fix: Fixed a bug that caused the wrong diagnostic message string to be used when "Generic" is used with no type arguments.
    -   Enhancement: Added new diagnostic message for when "Generic" is used in contexts outside of a class definition statement.
    -   Bug Fix (from Pylance): Use `sys.version_info` to query interpreter version.
    -   Enhancement: Added heuristics to type var solver so it picks the "least complex" solution when considering the elements within a union.
    -   Enhancement: Updated typeshed stubs to the latest versions.
    -   Bug Fix: Fixed a bug that caused an error to be reported when a newline token was used within an f-string expression.
        ([pylance-release#200](https://github.com/microsoft/pylance-release/issues/200))
    -   Enhancement: Added new diagnostic rule "reportInvalidStubStatement" (on by default in strict mode, off otherwise) that reports diagnostics for statements that should not appear within a type stub file.
    -   Enhancement: Added diagnostic for a module-level `__getattr__` function defined in a type stub file when in strict mode.
    -   Bug Fix: Fixed bug that caused imports (and other symbols) to be reported as unaccessed if they were accessed from within code that was deemed to be unreachable (e.g. due to the current platform configuration).
    -   Behavior Change: Changed logic for reportUnusedClass and reportUnusedFunction diagnostic rules so they don't report private-named functions and classes within stub files.
    -   Bug Fix: The token "..." should mean an ellipsis object, not the ellipsis class, when used in a normal expression within a non-stub file.
    -   Enhancement (from Pylance): Add python.analysis.autoImportCompletions to control auto-import completions.
-   [1.1.59](https://github.com/microsoft/pyright/releases/tag/1.1.59)
    -   Bug Fix: Changed the inferred type of an async function to use `Coroutine` rather than `Awaitable` type. `Coroutine` is a subclass of `Awaitable` and is arguably more correct in this case.
        ([pylance-release#184](https://github.com/microsoft/pylance-release/issues/184))
    -   Bug Fix: Fixed a bug in the handling of position-only parameters with default values followed by named parameters or \*\*kwargs.
    -   Bug Fix: Fixed a bug where "yield from" argument was assumed to be an "Iterator", but it should really be an "Iterable".
    -   Bug Fix: Fixed bug where "from .A import A" statement caused symbol "A" to have an inferred type that was a union of a module and other type, even though the other type immediately overwrites the module.
        ([pylance-release#188](https://github.com/microsoft/pylance-release/issues/188))
    -   Behavior Change: Changed type stub generator to never generate parameter type annotations based purely on default value types since those can be incorrect or incomplete. Changed type stub generator to automatically add method return types for common magic methods whose return type is always the same.
    -   Behavior Change: Changed type stub generator to avoid emitting functions and methods that begin with an underscore.
    -   Enhancement: Changed type checker to flag unaccessed symbols within type stubs in some cases. It doesn't mark function parameters or variables as unaccessed, and it doesn't mark imports of the form "from x import y as z" or "import a as b" as unaccessed since those are intended to be re-exports.
    -   Enhancement: Changed type checker to treat "..." as an "Unknown" type when used as the RHS of an assignment statement such as "a = ...". This idiom appears sometimes within type stubs, and it should be treated as a missing (unknown) type so stub authors know that they need to fill in a type annotation.
    -   Enhancement: Improved the diagnostic message used to report parameter type mismatches when a parameter name isn't known.
    -   Bug Fix: Fixed a bug whereby a TypeVar in a source type could be conflated with a same-named TypeVar in a dest type when performing TypeVar matching.
    -   Bug Fix: On the Windows platform, avoid calling 'python3' to determine the import paths for the current interpreter. This command can sometimes display a dialog indicating that python isn't installed and can be downloaded from the store.

## 2020.7.4 (29 July 2020)

-   Fixed case where analysis progress spinner would not disappear after analysis was complete.
-   Improved active parameter bolding in signature help for functions with multiple overrides.

In addition, Pylance's copy of Pyright has been updated from 1.1.54 to 1.1.58, including the following changes:

-   [1.1.58](https://github.com/microsoft/pyright/releases/tag/1.1.58)
    -   Enhancement: Rework signature help to use new VS Code / LSP APIs. Function overrides and active parameters are handled much, much better.
    -   Enhancement: Added strict-mode check for declared return types in type stubs, which don't allow for return type inference.
    -   Bug Fix: Fixed bug in type checker that resulted in a crash when a function declaration referred to itself within its return type annotation.
        ([pylance-release#181](https://github.com/microsoft/pylance-release/issues/181))
    -   Bug Fix: Fixed bug that caused duplicate diagnostics to be reported for quoted type annotations in some cases.
    -   Bug Fix: Fixed bug that caused "find all references" and "replace symbol" to sometimes miss references to a symbol if they were within quoted type annotations or type comments.
    -   Bug Fix: Fixed bugs in a few of the "find all references" tests, which were not properly quoting a forward-declared symbol.
    -   Bug Fix: Fixed a bug that caused infinite recursion and a crash when printing the type of a function that refers to itself within its own return type annotation.
        ([pylance-release#181](https://github.com/microsoft/pylance-release/issues/181))
    -   Bug Fix: Fixed bug where an f-string expression generated an error if it ended in an equal sign followed by whitespace. The Python 3.8 spec doesn't indicate whether whitespace is allowed here, but clearly the interpreter accepts it.
        ([pylance-release#182](https://github.com/microsoft/pylance-release/issues/182))
    -   Bug Fix: Fixed bug in logic that handles chained comparisons (e.g. `a < b < c`). The code was not properly handling the case where the left expression was parenthesized (e.g. `(a < b) < c`).
    -   Enhancement: Improved bidirectional type inference in the case where the type and the expected type are generic but the expected type is a base class that has been specialized. For example, if the expected type is `Mapping[str, int]` and the type is a `dict`.
-   [1.1.57](https://github.com/microsoft/pyright/releases/tag/1.1.57)
    -   Bug Fix: Fixed bug that caused partial type stub creation (for subpackages of a top-level package) to be generated in the wrong directory.
    -   Change in Behavior: Changed logic within type evaluator to track differences between None and NoneType. Previously, they were treated interchangeably. This worked in most cases, but there are some edge cases where the difference is important.
    -   Change in Behavior: Changed logic that converts a type to text so it properly distinguishes between "None" and "NoneType". It previously always output "None".
    -   Enhancement: Added support for NoneType matching a type expression `Type[T]` during TypeVar matching.
    -   Bug Fix: Fixed the handling of class or instance variable declarations that redefine a same-named symbol in an outer scope but do not use a variable declaration statement within the class.
        ([pylance-release#175](https://github.com/microsoft/pylance-release/issues/175))
    -   Bug Fix: Updated type checker's logic for dealing with symbols that are declared in an inner scope and an outer scope but used within the inner scope prior to being redefined.
    -   Bug Fix: Fixed bug a homogeneous tuple of indeterminate length was indexed with a constant expression.
    -   Enhancement: Made the reportIncompatibleMethodOverride rule smarter. It now properly handles position-only parameters and allows a subclass to extend the signature of a method it is overriding as long as the parameters are \*varg, \*\*kwarg, or have default values.
        ([pylance-release#157](https://github.com/microsoft/pylance-release/issues/157))
    -   Enhancement: Augmented the reportIncompatibleMethodOverride diagnostic rule to check for cases where a non-function symbol within a subclass redefines a function symbol in a base class.
    -   New Feature: Added new diagnostic rule "reportIncompatibleVariableOverride" which is similar to "reportIncompatibleMethodOverride" except that it reports incompatible overrides of variables (non-methods).
-   [1.1.56](https://github.com/microsoft/pyright/releases/tag/1.1.56)
    -   Bug Fix: Fixed bug that caused the default python platform not to be specified if there was no config file and no python interpreter selected.
    -   Bug Fix: Fixed crash in type checker that occurs when removing NoReturn from a union and having no remaining types.
    -   Bug Fix: Fixed bug that caused `__name__` not to be flagged as an invalid attribute on a class instance.
        ([pylance-release#154](https://github.com/microsoft/pylance-release/issues/154))
    -   Bug Fix: Fixed bug that caused quoted type annotation (i.e. a forward reference) that contains type arguments to report an "unbound symbol".
    -   Enhancement: Improved CompletionItemKind for intrinsic class symbols like `__name__`, etc.
        ([pylance-release#154](https://github.com/microsoft/pylance-release/issues/154))
    -   Bug Fix: Fixed bug in parsing of unicode named character encodings within string literals when the encoding included capital letters.
        ([pylance-release#161](https://github.com/microsoft/pylance-release/issues/161))
    -   Bug Fix: Fixed bug whereby a non-function definition (such as an instance variable) within a subclass was not considered as having overridden an abstract method or property within a base class.
    -   Change in Behavior: Changed Never internal type to be assignable to any type. Previously, it was assignable to no type.
    -   Bug Fix: Fixed bug that caused a spurious error during TypeVar matching when the TypeVar is constrained and is initially matched against an Any or Unknown type but is later matched against a known type.
    -   Bug Fix: Fixed bug in dataclass logic that reported spurious error when initializing attribute with `field(init=False)`.
        ([pylance-release#162](https://github.com/microsoft/pylance-release/issues/162))
    -   Change in Behavior: Renamed ParameterSpecification to ParamSpec to reflect latest PEP 612 changes.
    -   Enhancement: Updated typeshed fallback stubs to latest version.
    -   Change in Behavior: Updated PEP 612 and 614 features to be dependent on 3.10 rather than 3.9.
    -   Bug Fix: Fixed bug that caused diagnostics to persist in files that are not part of the workspace even after they are closed.
    -   Bug Fix: Fixed bug that generated incorrect type checking error when type alias used a `Type[x]` type annotation.
-   [1.1.55](https://github.com/microsoft/pyright/releases/tag/1.1.55)
    -   Bug Fix: Changed logic for reportMissingModuleSource diagnostic rule so it isn't reported for stub files.
    -   Enhancement: Added support for typing.OrderedDict, which is a generic alias for collections.OrderedDict.
        ([pylance-release#151](https://github.com/microsoft/pylance-release/issues/151))
    -   Enhancement: Added support for new Python extension callback so Pyright extension is notified when pythonPath is modified.
    -   Bug Fix: Fixed bug in docstring trimming code that resulted in some docstrings (those consisting of two lines where the second line was empty) not appearing when hovering over functions.
    -   Bug Fix: Fixed bug in type checker that resulted in incorrect error when creating a generic type alias with a compatible TypeVar as one of the type arguments.
    -   Bug Fix: Fixed bug that caused value expressions for default parameter values in lambdas to be evaluated within the wrong scope resulting in errors if the lambda scope had a same-named symbol.
    -   Bug Fix: Fixed bugs in handling of wildcard imports. First, it was not properly handling the implicit introduction of symbol A in the statement `from .A import *`. Second, it was implicitly including submodules as part of the wildcard, and it shouldn't.
    -   Bug Fix: Fixed bug that resulted in incorrect error when using an unpack operator in an argument expression that corresponds to a \*varg parameter in the callee.
    -   Bug Fix: Fixed recent regression that caused `isinstance` check to emit a bad error when `self.__class__` was passed as a second argument.

## 2020.7.3 (21 July 2020)

-   Fixed typo in marketplace entry's readme.

In addition, Pylance's copy of Pyright has been updated from 1.1.53 to 1.1.54, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Enhancement: Changed logic for reportMissingModuleSource diagnostic rule so it isn't reported for stub files.
    -   Enhancement: Added support for typing.OrderedDict, which is a generic alias for collections.OrderedDict.
        ([pylance-release#151](https://github.com/microsoft/pylance-release/issues/151))
    -   Bug Fix: Fixed bug in docstring trimming code that resulted in some docstrings (those consisting of two lines where the second line was empty) not appearing when hovering over functions.
    -   Bug Fix: Fixed bug in type checker that resulted in incorrect error when creating a generic type alias with a compatible TypeVar as one of the type arguments.
    -   Bug Fix: Fixed bug that caused value expressions for default parameter values in lambdas to be evaluated within the wrong scope resulting in errors if the lambda scope had a same-named symbol.
-   [1.1.54](https://github.com/microsoft/pyright/releases/tag/1.1.54)
    -   Enhancement: Added json schema for mspythonconfig.json (in addition to pyrightconfig.json).
    -   Enhancement: Updated config file watcher logic so it can detect when a new config file is added to a workspace.
    -   Bug Fix: "Find all references" should not return references to a symbol within library code unless that library source file is currently open in the editor.
    -   Bug Fix: Fixed bug in type checker that caused a crash when analyzing an abstract class with a constructor that contained two or more parameters, all of which are unannotated.
        ([pylance-release#118](https://github.com/microsoft/pylance-release/issues/118))
    -   Bug Fix: Fixed pyrightconfig.json JSON schema to accept "information" as a valid diagnostic severity setting.
    -   Enhancement: Updated log levels for messages logged by the Pyright service. Some log levels were "info" but should have been "warning" or "error".
        ([pylance-release#120](https://github.com/microsoft/pylance-release/issues/120))
    -   Bug Fix: Fixed bug that caused incorrect type evaluation for \*args or \*\*kwargs parameters if no type annotation was present. This bug also affected completion suggestions for these parameters.
        ([pylance-release#119](https://github.com/microsoft/pylance-release/issues/119))
    -   Bug Fix: Fixed a bug that resulted in Pyright attempting to parse and analyze binaries (native libraries) like ".pyd" and ".so" files.
        ([pylance-release#124](https://github.com/microsoft/pylance-release/issues/124))
    -   Bug Fix: Fixed bug in argument/parameter matching when an unpack operator is used in the argument and the parameter is a \*varg type.
    -   Enhancement: Renamed setting "pyright.useLibraryCodeForTypes" to "python.analysis.useLibraryCodeForTypes" for compatibility with Pylance. The older setting name is still supported but will be removed in the future.
    -   Enhancement: Added code to handle the case where a class is assigned to a type described by a callable protocol object. In this case, the class constructor's signature should be compared against the `__call__` signature defined in the protocol.
    -   Bug Fix: Fixed bug in import resolver that caused imports that referred to local namespace packages not to resolve.
    -   Bug Fix: Fixed bug that caused enum names that were not uppercase to be handled incorrectly.
    -   Bug Fix: Fixed bug that caused incorrect type analysis when a package `__init__.py` imported and re-exported a symbol that matched the submodule it was being imported from, e.g. `from .foo import foo`.
    -   Bug Fix: Fixed bug in type analyzer where default value expressions for lambda parameters were not being evaluated. This meant that errors related to these expressions were not reported, and symbols referenced within them were marked as unreferenced.

## 2020.7.2 (15 July 2020)

-   Allow find all references to search libraries if invoked from non-user files.

In addition, Pylance's copy of Pyright has been updated from 1.1.51 to 1.1.53, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Bug Fix: Updated config file watcher logic so it can detect when a new config file is added to a workspace.
-   [1.1.53](https://github.com/microsoft/pyright/releases/tag/1.1.53)
    -   Bug Fix: Fixed bug in parser where it was emitting a spurious error about function return type annotations being a tuple when it was simply enclosed in parentheses.
    -   Bug Fix: Fixed a bug that caused completion suggestions not to work for the LHS of a member access expression (e.g. the "a" in "a.b").
    -   Bug Fix: Fixed diagnostic message for "partially unknown" types (used in strict mode). It was incorrectly using the "unknown" message rather than "partially unknown", which could lead to confusion.
    -   Enhancement: Changed type printing logic to emit "Unknown" annotations when in strict mode to make it clearer to the user which part of the type is unknown.
    -   Bug Fix: Fixed bug that caused extension to report empty diagnostics for all tracked files when unnecessary (in particular, when the diagnostic mode is set to openFilesOnly and the file is not open).
    -   Enhancement: Added partial support for mypy-supported variant of "# type: ignore" comment where specific error codes in square brackets after the "ignore". Pyright doesn't honor the specific error codes but now treats it as a normal # type: ignore" comment.
        ([pylance-release#108](https://github.com/microsoft/pylance-release/issues/108))
    -   Bug Fix: Fixed bug that caused the arguments of a call to remain unanalyzed if the LHS of the call was not callable. This resulted in omitted errors and spurious unreferenced symbols.
    -   Bug Fix: Changed diagnostic for second argument to "Enum" call to be dependent on the reportGenalTypeIssues diagnostic rule rather than unconditional.
    -   Bug Fix: Fixed recent regression relating to "isinstance" type narrowing when the type of the target is a constrained TypeVar.
    -   Bug Fix: Fixed bug in the handling of the NewType function introduced in PEP 484. The previous code was not synthesizing a constructor (`__init__` method) as specified in the PEP.
    -   Enhancement: Changed fallback mechanism for detecting the configured python interpreter to use the shell command "python3" first and then "python" if that fails. This is preferable on Linux and MacOS because "python" typically points to a Python 2.7 interpreter.
    -   Enhancement: Added parser error for relative imports of the form "import .abc". This is treated as a syntax error by the Python interpreter and should be flagged as such.
    -   Bug Fix: Fixed bug with "from . import a" form of import. Diagnostic was not logged when "a" could not be resolved.
-   [1.1.52](https://github.com/microsoft/pyright/releases/tag/1.1.52)
    -   Bug Fix: Fixed escaping of literal strings when printing Literal string types.
    -   Enhancement: Improved completion suggestions related to member access expressions (e.g. obj.method) by binding the method to the object when appropriate.
    -   Enhancement: When hovering over class name that is used in a constructor call, display the `__init__` method signature rather than the class.
    -   Bug Fix: Fixed recent regression in unreachable code reporting at the module level.
        ([pylance-release#107](https://github.com/microsoft/pylance-release/issues/107))
    -   Bug Fix: Removed error message for unexpected dynamic argument types to `type` initializer.
        ([pylance-release#114](https://github.com/microsoft/pylance-release/issues/114))
    -   Bug Fix: Fixed a bug in the code that validates an exception type in an "except" clause. It was not properly handling the case where the type of the exception was specified as a `Type[X]` object.
    -   Bug Fix: Reverted part of a previous change where constrained type vars were specialized as a union of the constrained types. Changed logic to use first constrained type only.
    -   Bug Fix: Fixed bug in logic that detects assignment compatibility for function types. It wasn't properly handling generic parameters, including synthesized TypeVar types used for "self" parameters.
    -   Bug Fix: Added diagnostic for TypeVar or generic class with type args being used as a second argument for isinstance or issubclass. These will raise a TypeError exception at runtime.
    -   Enhancement: Changed Pyright import resolution order to match that described in PEP 561. In particular, stubs in stubPath are now searched prior to user code, and third-party typeshed stubs are searched only after installed packages are searched for stub packages and inline stubs. There is one place where Pyright's import resolution still differs from a strict interpretation of PEP 561: it searches stdlib typeshed stubs first (unless typeshedPath is defined, in which case it searches there). This is more consistent with the way the Python interpreter resolves stdlib types.
    -   Bug Fix: Fixed bug in handling of constructor that uses a specialized class (e.g. `MyClass[int]()`). The previous code was inappropriate overriding the provided type arguments as part of bidirectional inference logic.
    -   Bug Fix: Fixed bug that caused spurious errors when assigning a specialized object/class to a variable whose type is a specialized base class of the specialized object/class.

## 2020.7.1 (10 July 2020)

-   Fixed background analysis thread, which prevented diagnostics (syntax checks, import warnings, etc) from working.
    ([pylance-release#86](https://github.com/microsoft/pylance-release/issues/86))
-   Fixed setting and survey banners blocking startup.

## 2020.7.0 (9 July 2020)

-   Hovers for class invocations will now show the `__init__` method's docstring.
-   Import organization has been disabled to prevent conflicts with the Python extension's import sorting.
    ([pylance-release#23](https://github.com/microsoft/pylance-release/issues/23))
-   Docstrings for bound methods will no longer show `self` in the signature.
-   Fixed multi-line string literals in tooltips.
-   IntelliCode now operates in environments without OpenMP.
-   The `pandas` stubs have been improved.
    ([pylance-release#13](https://github.com/microsoft/pylance-release/issues/13), [pylance-release#71](https://github.com/microsoft/pylance-release/issues/73), [pylance-release#73](https://github.com/microsoft/pylance-release/issues/71))
-   `pyplot.subplots`'s signature has been fixed.
    ([pylance-release#43](https://github.com/microsoft/pylance-release/issues/43))
-   The bundled copy of typeshed has been updated.
-   The overall startup time and responsiveness has been improved.

In addition, Pylance's copy of Pyright has been updated from 1.1.46 to 1.1.51, including the following changes:

-   Unreleased in Pyright, but included in Pylance:
    -   Bug Fix: Fixed recent regression in unreachable code reporting at the module level.
    -   Enhancement: Removed error message for unexpected dynamic argument types to `type` initializer.
-   [1.1.51](https://github.com/microsoft/pyright/releases/tag/1.1.51)
    -   New Feature: Added document highlight provider. When you click on a symbol in the editor, all other symbols within the same file that have the same name and have the same semantic meaning are also highlighted.
        ([pylance-release#42](https://github.com/microsoft/pylance-release/issues/42))
    -   Enhancement: If reportGeneralTypeIssues rule is disabled, don't replace assigned type with declared type because it will lead to additional errors that will confuse users.
        Enhancement: Added type narrowing support for "in" operator when RHS is a specialized list, set, frozenset, or deque.
    -   Enhancement: Added logic to validate that RHS operand used with "in" and "not in" operators support the `__contains__` magic method.
    -   Bug Fix: Fixed bug where "field" initialization of dataclass member didn't take into account "default" or "default_factory" parameters when determining whether the field had a default value.
    -   Bug Fix: Added code to deal with the special case where a method declared with a "def" statement is later overwritten with a callable instance variable.
    -   Bug Fix: Fixed bug whereby a TypeVar type was not treated the same when it was alone versus within a union leading to some subtle differences in error reporting. Also changed specialization of constrained TypeVars to be a union of constrained types rather than Unknown if the TypeVar is not used as a type argument.
    -   Bug Fix: Fixed bug in diagnostic message for constrained TypeVar type mismatch. The wrong type was being printed leading to confusing errors.
    -   Bug Fix: Fixed a bug that caused incorrect linearization of classes during MRO calculation.
    -   Bug Fix: Fixed bug in synthesized version of `get` method for `TypedDict` class. It should provide an overload that allows for any str key and return an "Unknown" type.
-   [1.1.50](https://github.com/microsoft/pyright/releases/tag/1.1.50)
    -   Bug Fix: Fixed regression in completion provider when retrieving suggestions for "self.". Added test to cover this case.
        ([pylance-release#53](https://github.com/microsoft/pylance-release/issues/53))
    -   Enhancement: Changed "x is not iterable" diagnostic to be part of the "reportGeneralTypeIssues" rule so it doesn't get reported if typeCheckingMode is "off".
        ([pylance-release#59](https://github.com/microsoft/pylance-release/issues/59))
    -   Bug Fix: Fixed bug that caused incorrect behavior when a symbol was imported multiple times in the same file.
    -   Bug Fix: Fixed bug that caused Callable instance variables to be treated as though they needed to be "bound" to the object at the time they were accessed. This resulted in spurious errors about parameter count because an implicit "self" parameter was assumed.
    -   Enhancement: Improved type analysis performance by 5-10% on typical code and by significantly more on certain code sequences that involve many if statements within a loop. This optimization uses code flow caching to determine when incomplete types (those that haven't been fully resolved) are potentially stale.
        ([pylance-release#57](https://github.com/microsoft/pylance-release/issues/57))
    -   Bug Fix: Fixed recent regression related to imports of the form "from .x import y" within an `__init__.py(i)` file.
    -   Enhancement: Changed type analyzer to use module-level `__getattr__` for types only if the file is a stub.
    -   Enhancement: Added code to prevent "variable possibly unbound" error from propagating to other variables. It should be reported only once.
    -   Enhancement: Switched "pyright.typeCheckingMode" to "python.analysis.typeCheckingMode" for compatibility with Pylance.
    -   Enhancement: Moved a few parameter-related diagnostics to the "reportGeneralTypeIssues" diagnostic rule rather than being unconditional errors.
        ([pylance-release#15](https://github.com/microsoft/pylance-release/issues/15), [pylance-release#39](https://github.com/microsoft/pylance-release/issues/39), [pylance-release#54](https://github.com/microsoft/pylance-release/issues/54))
    -   Bug Fix: Fixed bug that resulted in incorrect type inference for a member variable that is not assigned within a class but is assigned within an ancestor class.
    -   Enhancement: Added type narrowing support for "is" and "is not" operator where RHS is an enum literal value.
-   [1.1.49](https://github.com/microsoft/pyright/releases/tag/1.1.49)
    -   Bug Fix: Fixed bug that caused incorrect type to be determined for \*args and \*\*kwargs parameters in some contexts.
        ([pylance-release#20](https://github.com/microsoft/pylance-release/issues/20))
    -   Enhancement: Updated typeshed stubs to the latest versions from the typeshed repo.
    -   Bug Fix: Fixed bug in tokenizer where it was generating an error if escaped unicode characters (using the \N{name} escape) contained a space in the name.
        ([pylance-release#25](https://github.com/microsoft/pylance-release/issues/25))
    -   Enhancement: Improved parse recovery for statements that are supposed to end in a colon followed by a suite of other indented statements. Previously, a missing colon or expression error resulted in a cascade of additional errors.
        ([pylance-release#22](https://github.com/microsoft/pylance-release/issues/22))
    -   Enhancement: Improved error message for overloaded calls where no overload matches the provided arguments.
    -   Bug Fix: Fixed bug in unreachable code detection and reporting. The logic was previously split between the binder (which used proper code flow analysis) and the checker (which didn't use code flow analysis but had access to NoReturn - call information). The new code combines everything into the checker and uses both code flow analysis and NoReturn call info.
        ([pylance-release#31](https://github.com/microsoft/pylance-release/issues/31))
    -   Bug Fix: Added code to include a symbol in a module if the source file is an `__init__.py(i)` and a relative import of the form "from .x.y.z import X" is used. In this case, the symbol "x" should appear within the module's namespace.
    -   Bug Fix: Fixed bug in pyrightconfig schema. The defaults for several settings were using strings "true" and "false" rather than booleans true and false.
    -   Bug Fix: Fixed bug in parser that generated a spurious error when an unparenthesized assignment expression (walrus operator) was used as an argument. PEP 572 indicates that this should be allowed in cases where the argument is not named.
    -   Enhancement: Changed constructor type analysis logic to always specialize the instantiated instance.
    -   Bug Fix: Fixed bug in reportAssertAlwaysTrue diagnostic. It wasn't properly handling tuples of indeterminate length.
    -   Bug Fix: Fixed bug in import resolution that resulted in an unresolved import when a local folder was present with the same name as the imported third-party library.
    -   Bug Fix: Fixed bug that caused diagnostics for unopened files to remain in "problems" panel after switching diagnostic mode from "workspace" to "open files only".
    -   Bug Fix: Fixed bug in parsing of f-string expressions that contain nested braces.
        ([pylance-release#45](https://github.com/microsoft/pylance-release/issues/45))
    -   Bug Fix: Fixed bug in import resolver where it was not preferring regular package imports over namespace packages.
        ([pylance-release#52](https://github.com/microsoft/pylance-release/issues/52))
-   [1.1.48](https://github.com/microsoft/pyright/releases/tag/1.1.48)
    -   Enhancement: Added support for accessing metaclass members from class. This allows, for example, access to the `__members__` attribute of an Enum class.
    -   Enhancement: Added type completion support for class attributes provided by a metaclass.
    -   Bug Fix: Fixed bug that caused unbound variables to go unreported if they had type annotations.
    -   Bug Fix: Fixed bug in type narrowing logic for isinstance call. It wasn't properly handling bound TypeVar types. This includes synthesized bound TypeVars like those used for unannotated "self" and "cls" parameters.
    -   Bug Fix: Fixed bug that caused stand-alone expression statements (those that are not included in other statements) to go unchecked, resulting in symbols potentially unreferenced and type errors unreported.
    -   Bug Fix: Fixed bug where the use of unpack operator within a tuple not surrounded by parens within a return/yield statement incorrectly reported an error when used with Python <3.8.
    -   Bug Fix: Changed signature help provider to use the `__init__` method signature (if available) for class construction expressions. It previously used the `__new__` method signature by default.
    -   Enhancement: Unaccessed function parameters are now displayed as "grayed out" in VS Code. There was previously code in place to do this, but it contained a bug that went unnoticed.
-   [1.1.47](https://github.com/microsoft/pyright/releases/tag/1.1.47)
    -   Enhancement: Improved support for type aliases, especially those with generic parameters. Type alias names are now tracked and used within printed type names.
    -   Bug Fix: Fixed recent regression in CLI that resulted in unintended verbose logging output.
    -   Bug Fix: Added minimum node version to package.json to prevent installation of pyright CLI on incompatible versions of node.
    -   Enhancement: Added code to better handle the obsolete "<>" operator from Python 2 - including a better error message and better parse recovery.
    -   Enhancement: Added special-case handling of 'NoReturn' type to allow Never type to be assigned to it. This can be used to verify exhaustive type narrowing.
    -   Bug Fix: Added code to differentiate between Protocol symbol in typing.pyi versus typing_extensions.pyi. The latter can be used on older versions of Python.
    -   Enhancement: Changed activation events to remove glob path for pyrightconfig.json, which speeds up extension activation on large projects. Added support for mspythonconfig.json

## 2020.6.1 (30 June 2020)

Initial release!
