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
complete Windows payload only after the required tests and release gate pass.

The workflow creates no installer before testing. The passed payload contains
the receipt needed by the MSI/Setup packaging gates. A Windows Server runner
checks the native console APIs and scripted editor behavior; Windows 11 terminal
appearance and an interactive installer still need their own platform checks.
