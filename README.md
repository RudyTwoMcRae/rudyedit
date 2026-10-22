# rudyedit
A console based text editor with a complete development environment included.

The application source, original application license, specifications, assets,
packaging tools and test scripts are in [RudyEdit](RudyEdit/README.md).
The repository's existing root LICENSE is preserved; the application's original
license notice remains at RudyEdit/LICENSE.

[Windows executable tests](https://github.com/RudyTwoMcRae/rudyedit/actions/workflows/windows-tests.yml) run for
source pushes, pull requests and manual Actions dispatch. The workflow builds
and tests the actual self-contained Windows executable on a native Windows
runner. It retains complete test logs even when tests fail and uploads the
complete Windows payload after verifying the completed native test attempt and
its byte fingerprint. A second job builds the internal MSI and single Setup EXE
even when tests fail. Main-branch builds publish Setup, checksums and full reports
as a GitHub release (a prerelease when tests fail). Test failures still fail the
Windows test job and overall workflow. Untested/stale payloads cannot be packaged.

A Windows Server runner checks native console APIs and scripted editor behavior;
interactive Windows 11 installation, upgrade, uninstall and terminal appearance
require separate validation.
