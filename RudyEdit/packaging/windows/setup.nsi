Unicode true
!ifndef PYTHON
!define PYTHON "python3"
!endif
!system '${PYTHON} "../../tests/release_gate.py" "../../dist/win-x64"' = 0
!include "MUI2.nsh"
Name "RudyEdit 2.0.5"
OutFile "../../dist/RudyEdit-2.0.5-Windows-Setup.exe"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Var DesktopArg
!define MUI_ICON "../../assets/rudyedit.ico"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "../../LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TEXT "RudyEdit is installed.$\r$\n$\r$\nFor graphical menu and file icons, select FiraCode Nerd Font Mono in your terminal profile font settings. Restart your terminal to refresh fonts and PATH.$\r$\n$\r$\nHelp > Nerd Font & Icons contains setup instructions. View > Toggle Graphic Icons enables plain labels."
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "English"
Function .onInit
  StrCpy $DesktopArg "0"
FunctionEnd
Function .onInstFailed
  ClearErrors
  FileOpen $1 "$TEMP\RudyEdit-install.log" a
  ${IfNot} ${Errors}
    FileWrite $1 "RudyEdit Setup 2.0.5 failed. Review the installer details and any preceding Windows Installer entries.$\r$\n"
    FileClose $1
  ${EndIf}
FunctionEnd
Section /o "Create a Desktop shortcut" DesktopOption
  StrCpy $DesktopArg "1"
SectionEnd
Section "RudyEdit application, runtime, font and Start Menu entry (required)"
  SectionIn RO
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File /oname=RudyEdit.msi "../../dist/windows-internal/RudyEdit.msi"
  DetailPrint "Installing RudyEdit with Windows Installer..."
  ClearErrors
  ExecWait '"$SYSDIR\msiexec.exe" /i "$PLUGINSDIR\RudyEdit.msi" /passive /norestart /L*V "$TEMP\RudyEdit-install.log" DESKTOP_SHORTCUT=$DesktopArg' $0
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "Windows Installer could not be started. Details are written to $TEMP\RudyEdit-install.log."
    SetErrorLevel 1
    Abort
  ${EndIf}
  ${If} $0 == 3010
    SetRebootFlag true
  ${ElseIf} $0 != 0
    MessageBox MB_ICONSTOP "Windows Installer returned error $0. Details were written to $TEMP\RudyEdit-install.log."
    SetErrorLevel $0
    Abort
  ${EndIf}
SectionEnd
