# Security Events and Sensitive Error Messages - Assessment Evidence

**Work Item**: [ADO #2984085](https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2984085)
**Assessment Date**: 2026-07-03
**Status**: PARTIAL - repo-backed evidence and targeted hardening added; owner review still required before claiming full compliance

## Summary

PTVS has repo-backed evidence for telemetry/event logging, unhandled-exception reporting, ActivityLog/EventLog integration, and non-fatal logging paths. PTVS is primarily Visual Studio client functionality, so this control is interpreted as: product security-relevant events and failures should be logged for diagnostics, while user-facing errors and diagnostic logs should avoid exposing secrets or unnecessary sensitive internals.

This assessment found several existing logging and error-handling mechanisms. It also added targeted redaction for common secret patterns in central C# diagnostic exception output and user-visible task-dialog details. This does not prove full compliance for every security-sensitive flow and does not include runtime logs, telemetry dashboards, Watson records, or owner attestation that all user-visible errors are free of sensitive data.

## Test Case 571: Log security events and do not expose security-sensitive errors/messages - PARTIAL

**Assessor Request**: Initial submission. The work item has no existing comments.

### Evidence 1: Telemetry logging is opt-in and structured

**Location**: `Python/Product/PythonTools/PythonTools/Logging/VsTelemetryLogger.cs`

PTVS telemetry is posted through Visual Studio telemetry only when a session exists and the user has opted in. The implementation prefixes Python events consistently and avoids logging repeated package names.

```csharp
public void LogEvent(PythonLogEvent logEvent, object argument) {
    // No session is not a fatal error.
    // Never send events when users have not opted in.
    if (_session.Value == null || !_session.Value.IsOptedIn) {
        return;
    }

    // Certain events are not collected
    switch (logEvent) {
        case PythonLogEvent.PythonPackage:
            lock (_seenPackages) {
                var name = (argument as PackageInfo)?.Name;
                // Don't send empty or repeated names
                if (string.IsNullOrEmpty(name) || !_seenPackages.Add(name)) {
                    return;
                }
            }
            break;
    }

    var evt = new TelemetryEvent(EventPrefix + logEvent.ToString());
```

**Control coverage**:

- Events are structured under the `vs/python/` prefix.
- Telemetry respects Visual Studio opt-in state.
- Repeated or empty package-name telemetry is filtered.

### Evidence 2: Unhandled exceptions are reported to trace, EventLog, ActivityLog/UI with de-duplication

**Location**: `Python/Product/VSCommon/Infrastructure/VSTaskExtensions.cs` and `Python/Product/Common/Infrastructure/ExceptionExtensions.cs`

Unhandled exception handling centralizes task exception reporting. Non-critical task exceptions are converted to diagnostic messages, traced, written to Windows EventLog where possible, and de-duplicated before user display.

```csharp
var message = ex.ToUnhandledExceptionMessage(callerType, callerFile, callerLineNumber, callerName);

// Send the message to the trace listener in case there is
// somebody out there listening.
Trace.TraceError(message);
```

```csharp
try {
    result = await task;
} catch (Exception ex) {
    if (task.IsFaulted) {
        if (ex.IsCriticalException()) {
            throw;
        }

        ex.ReportUnhandledException(site, callerType, callerFile, callerLineNumber, callerName, allowUI);
    }
}
```

**Control coverage**:

- Security-relevant/failure events are not silently swallowed in the common async path.
- Event logging failures are contained so logging failures do not destabilize the product.
- Critical exceptions are rethrown rather than hidden.
- Central exception-message output is sanitized for common secret patterns before being returned to trace/EventLog/debug consumers.

### Evidence 3: User-visible task dialog details are sanitized for common secret patterns

**Location**: `Python/Product/VSCommon/Infrastructure/TaskDialog.cs`

Task-dialog expanded exception details are passed through `SensitiveDataRedactor.Sanitize(...)` before display. Retry dialogs also sanitize exception messages and expanded exception details.

**Control coverage**:

- User-visible expanded exception details avoid common secret-bearing key/value patterns, authorization headers, and URI user-info credentials.
- The redaction is deterministic and does not suppress exception reporting.

### Evidence 4: Command failures are logged and command reuse is disabled after unexpected errors

**Location**: `Python/Product/PythonTools/PythonTools/Project/CustomCommand.cs`

Custom command execution disables the command after unexpected errors and logs to ActivityLog. ActivityLog messages added through these paths are sanitized before logging.

```csharp
// Prevent the command from executing again until the project is
// reloaded.
_canExecute = false;
```

**Control coverage**:

- Prevents repeated execution after an unexpected command failure.
- Logs diagnostic data for investigation.
- Redacts common secret patterns before ActivityLog output for the covered custom-command failure paths.

### Evidence 5: FastCGI helper logs request/runtime failures without breaking request handling

**Location**: `Python/Product/WFastCgi/wfastcgi.py`

WFastCGI logs to Application Insights when configured and to `WSGI_LOG` when present. Logging failures are intentionally non-fatal; unhandled exceptions in the FastCGI loop are logged. Log text is sanitized before it is sent to either sink.

```python
def maybe_log(txt):
    """Logs messages to a log file if WSGI_LOG env var is defined, and does not
    raise exceptions if logging fails."""
    try:
        log(txt)
    except:  # nosec B110
        pass  # nosec B110 - maybe_log intentionally suppresses logging failures.
```

**Control coverage**:

- Runtime failures are logged when logging is configured.
- Logging failures do not break request processing.
- Unhandled FastCGI exceptions are captured for investigation.
- Common secret-bearing key/value patterns, authorization headers, and URI user-info credentials are redacted before configured FastCGI logging sinks receive the message.

## Owner Review Needed

- Confirm whether PTVS has a documented list of security events that must be logged for Visual Studio client functionality.
- Confirm whether any error dialogs, output-window messages, ActivityLog entries, telemetry events, or FastCGI logs may contain secrets, tokens, credentials, customer content, full local paths, or other sensitive data outside the covered redaction paths.
- Confirm whether WFastCGI exception stack traces are acceptable for configured server logs, or whether additional redaction is required.
- Confirm whether telemetry properties and Watson records require a separate allowlist or scrub pass.

## Draft ADO Comment

```markdown
## Security events and sensitive errors - Evidence Report

**Assessment Date:** 2026-07-03
**Status:** PARTIAL - owner review required for complete sensitive-data and security-event coverage

### Evidence

- `Python/Product/PythonTools/PythonTools/Logging/VsTelemetryLogger.cs` posts structured Python telemetry only when Visual Studio telemetry is available and the user has opted in.
- `Python/Product/VSCommon/Infrastructure/VSTaskExtensions.cs` centralizes unhandled task exception reporting to trace/EventLog and rethrows critical exceptions.
- `Python/Product/Common/Infrastructure/ExceptionExtensions.cs` now sanitizes central unhandled-exception diagnostic messages for common secret patterns.
- `Python/Product/Cookiecutter/Shared/Infrastructure/ExceptionExtensions.cs` now sanitizes the duplicated Cookiecutter unhandled-exception formatter for the same common secret patterns.
- `Python/Product/VSCommon/Infrastructure/TaskDialog.cs` now sanitizes user-visible expanded exception details and retry-dialog exception text.
- `Python/Product/PythonTools/PythonTools/Project/CustomCommand.cs` logs unexpected custom-command failures to ActivityLog, disables the command until reload, and sanitizes covered ActivityLog messages.
- `Python/Product/WFastCgi/wfastcgi.py` logs WSGI/FastCGI runtime failures to configured logs/Application Insights, keeps logging failures non-fatal, and sanitizes log text before both configured sinks.

### Owner-reviewed limitations

- Need owner confirmation that product error messages/logs do not expose secrets or other sensitive data outside the newly covered redaction paths.
- Need owner confirmation that security-event coverage is sufficient for Visual Studio client functionality.
- Need owner confirmation for telemetry dashboard, Watson record, and WFastCGI stack-trace treatment.

Evidence file: `.github/compliance/evidence/reports/security/2984085-security-events-sensitive-errors-evidence-2026-07-03.md`

*written by [comply](https://aka.ms/comply), reviewed by Bill*
```