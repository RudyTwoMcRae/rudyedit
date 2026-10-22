@echo off
setlocal
set "RUDYEDIT_TEST_LOG=%APPDATA%\RudyEdit\test-results\RudyEdit-tests.log"
python "%~dp0release_runner.py" --executable "%~dp0..\rudyedit.exe" --report "%RUDYEDIT_TEST_LOG%"
set "RUDYEDIT_TEST_EXIT=%ERRORLEVEL%"
echo.
echo Test log: %RUDYEDIT_TEST_LOG%
pause
exit /b %RUDYEDIT_TEST_EXIT%
