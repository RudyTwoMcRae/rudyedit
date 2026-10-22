# RudyEdit release requirements

For every revision, including fixes and rebuilds:

1. Update/add outcome tests for every changed or new user-facing command. The command catalog coverage check must pass in Code and VIM modes.
2. Build the test project, publish the target executable, and run tools/stage_release_tests.py on the publish folder.
3. Run tests/release_runner.py against that exact executable on its native OS after publication. Keep the completed text/JSON reports. They must start with totals, then errors, then the full named-test list.
4. Pass tests/release_gate.py before making any installer. MSI, NSIS and pacman entry points repeat this requirement. Never bypass it, forge a pass, count a blocked test as passed, or substitute a Linux run for Windows execution.
5. If any payload file changes, rerun the suite before packaging. Ship the same tested bytes, scripts and reports. Fix test/product defects rather than disabling failing checks.
6. If native execution/dependencies are unavailable, provide the blocked report and a validation bundle, clearly say which installer could not be created, and leave its gate closed.

Tests use temporary settings/documents. Preserve the MSI 2235 Font-table fix, permanent UpgradeCode, and the single-public-Windows-Setup policy.
