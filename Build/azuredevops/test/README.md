# PTVS P0 pull request gates

`PTVS-Build` runs an approved fast subset of the `Priority=0` tests from `PythonToolsTests.dll` for every pull request. It excludes the `10s` category plus `FormatSelectionBlack` and `RunInterpreterOutput`, which depend on mutable external tool or interpreter state. The gate requires all 58 selected tests to be discovered and executed, and any failed test or missing TRX file fails the build.

`PTVS-Testgate` consumes a successful `build` stage from `PTVS-Build` and runs the 123 `Priority=0` tests from `PythonToolsUITestsRunner.dll` in an installed Visual Studio instance. It uses the isolated `DDRITS-Public` lab and only downloads or executes source-build artifacts when Azure Pipelines reports `pr.isFork=False`. Fork pull requests complete the classification stage without accessing the bootstrapper, internal test lab, or source artifacts; their required coverage is the fast P0 gate in `PTVS-Build`.

## Azure DevOps and branch policy activation

The repository YAML cannot enable pipeline definitions or change repository branch policies. After this change is merged:

1. Enable the existing `PTVS-Testgate` pipeline (definition ID `27846`) and authorize its `PTVS-Build`, `DartLab`, `DartLab.Templates`, and DartLab service-connection resources on the first trusted run.
2. Confirm a same-repository pull request publishes `MicroBuildOutputs`, `Binaries`, `TestData`, `RunSettings`, and `P0RunSettings`, then passes both P0 gates.
3. Confirm a fork pull request runs only `Classify source build` in `PTVS-Testgate` and still passes the fast P0 gate in `PTVS-Build`.
4. Add the `PTVS-Testgate` status as a required validation for `main` only after both checks above succeed. Keep `PTVS-Build` required for all pull requests.

When intentionally changing either approved P0 inventory, update the corresponding minimum count in `Build/templates/run_p0_tests.yml` or `azure-pipelines-integration-testsgate.yml` in the same pull request.
