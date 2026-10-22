# RudyEdit release requirements

For every revision, including fixes and rebuilds:

1. Update/add outcome tests for every changed or new user-facing command. The command catalog coverage check must pass in Code and VIM modes.
2. Build the test project, publish the target executable, and run tools/stage_release_tests.py on the publish folder.
3. Run tests/release_runner.py against that exact executable on its native OS after publication. Keep the completed text/JSON reports. They must start with totals, then errors, then the full named-test list.
4. Per the October 22, 2026 user instruction, always create the Windows Setup EXE after a completed native Windows test attempt, even when tests fail or block. MSI and NSIS use release_gate.py --allow-test-failures to verify the exact tested bytes and completed native attempts. Keep failed/blocked results unchanged, publish reports, and retain a failing CI test status. Linux/pacman still requires a full pass. Never forge a pass or substitute a Linux run for Windows execution.
5. If any payload file changes, rerun the suite before packaging. Ship the same tested bytes, scripts and reports. Fix test/product defects rather than disabling failing checks.
6. Missing native test attempts, stale receipts and build/packaging errors remain errors. Publish diagnostics when available. After publishing a Windows installer with failures, attempt repairs, rerun native tests, and rebuild/publish the installer.

Tests use temporary settings/documents. Preserve the MSI 2235 Font-table fix, permanent UpgradeCode, and the single-public-Windows-Setup policy.
