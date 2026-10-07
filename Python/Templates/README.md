# Updating PTVS Templates

`Python/Templates` contains project and item templates that are packaged into PTVS template VSIXes. It is separate from the Cookiecutter integration in `Python/Product/Cookiecutter` and the Cookiecutter fixtures in `Python/Tests/TestData/Cookiecutter`.

## Template asset signing

Visual Studio insertion SignCheck scans files after extracting the template VSIX payloads. Passing the PTVS signing build does not guarantee that every extracted file type satisfies insertion policy.

Do not add TTF files to `Python/Templates`. The legacy Bootstrap templates intentionally use the existing EOT, SVG, and WOFF glyphicons fallbacks. TTF files previously submitted with `3PartyScriptsSHA2` still appeared as unsigned to insertion SignCheck.

Do not restore TTF signing, font catalogs, or packaged-font verification to work around this constraint. If a template refresh introduces a new payload type that SignCheck evaluates, confirm the supported Visual Studio insertion policy before adding signing logic.

Template JavaScript remains staged and signed through `Python/Setup/signlayout.proj`.

## Updating templates

After refreshing project templates, item templates, Bootstrap assets, or Cookiecutter-related content:

1. Determine whether the change affects `Python/Templates` and therefore a template VSIX payload.
2. Remove TTF assets and `format('truetype')` CSS fallbacks from refreshed content.
3. Update `.vstemplate` and generated project files so they do not reference removed assets.
4. Run `Build\ValidateTemplateAssets.ps1` from the repository root.
5. Build the applicable template VSIX or installer and inspect its packaged contents.
6. Use Visual Studio insertion SignCheck as the authoritative acceptance gate for extracted payload signatures.

The PTVS build runs `Build\ValidateTemplateAssets.ps1` before compiling the product, so violations fail with a reference to this document.
