---
name: ptvs-template-updates
description: Use when updating PTVS project or item templates, Cookiecutter integration, Bootstrap or other static template assets, or template VSIX packaging and signing.
---

# PTVS Template Updates

Read [`Python/Templates/README.md`](../../../Python/Templates/README.md) before changing template content or packaging. Treat it as the source of truth for template asset and insertion-signing requirements.

## Scope

Distinguish these template systems before editing:

- `Python/Templates` contains project and item templates packaged into PTVS template VSIXes.
- `Python/Product/Cookiecutter` contains the Cookiecutter integration and feed handling.
- `Python/Tests/TestData/Cookiecutter` contains Cookiecutter test fixtures.

Do not assume a Cookiecutter integration update changes the template VSIX payloads. Trace which files are packaged.

## Update checks

Run the repository guard after changing templates or static assets:

```powershell
Build\ValidateTemplateAssets.ps1 `
    -TemplateRoot Python\Templates `
    -DocumentationPath Python\Templates\README.md
```

Also confirm the Web and Django template VSIX projects still package from the staged template tree during signed builds, then run the smallest applicable template or installer build.
