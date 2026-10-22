# Windows testing in GitHub Actions

Repository: https://github.com/RudyTwoMcRae/rudyedit

The application is under RudyEdit/ in the repository so its source and original
license are preserved together. The existing root repository license is retained.
The workflow is .github/workflows/windows-tests.yml at the repository root.

Pushes affecting the application or workflow, pull requests, and manual dispatch
start a Windows job. It installs .NET SDK 10.0.100 and Python 3.12, calls
tools/build.ps1 -Rid win-x64, and repeats the exact-payload gate. That script
builds tests, publishes the self-contained executable with runtime 10.0.13,
stages the test runner/scripts, and tests the actual executable including native
Windows console API checks. Every command must have passing outcome coverage.

Full text/JSON test reports and subprocess output are uploaded even on failure.
The job summary starts with the test totals. A complete Windows payload artifact
is uploaded only after success. Its test receipt is bound to those bytes; use
that payload unchanged for later installer creation. Hash ordering is explicitly
based on relative path text so Windows-created receipts remain verifiable on a
Linux packaging host. Reports/caches are excluded from the payload hash.

The workflow itself needs only repository contents-read permission. Uploading or
editing its source from ChatGPT requires the connected GitHub account/app to have
repository contents-write and workflow-file write permission. An account with
read-only repository access cannot install the workflow.

GitHub-hosted windows-2022 is a native Windows Server runner, not a Windows 11
terminal GUI. Native console/API checks are automated; interactive Windows 11
font rendering and installation/upgrade/uninstallation checks remain separate.
No installer is produced by this test-only workflow. Existing installer builders
continue to reject missing, failed, blocked or stale executable test reports.
