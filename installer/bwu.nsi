; BWU installer
; Build: run build.ps1 first (creates ..\publish), then:  makensis installer\bwu.nsi
Unicode True
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

!define APPNAME   "BWU"
!define FULLNAME  "Better Windows Utilities"
!define VERSION   "1.0.0"
!define PUBLISHER "BWU Project"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\BWU"

; makensis cannot create the output folder itself
!system 'if not exist "${__FILEDIR__}\..\dist" mkdir "${__FILEDIR__}\..\dist"'

SetCompressor /SOLID lzma
Name "${APPNAME} - ${FULLNAME}"
OutFile "..\dist\BWU-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\BWU"
InstallDirRegKey HKLM "Software\BWU" "InstallDir"
RequestExecutionLevel admin

!define MUI_ICON   "..\BWU\Assets\Icons\bwu.ico"
!define MUI_UNICON "..\BWU\Assets\Icons\bwu.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\BWU.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start BWU"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "PortugueseBR"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "BWU requires a 64-bit version of Windows 10 or Windows 11."
    Abort
  ${EndIf}
  SetRegView 64
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd

Section "Install"
  SetShellVarContext all
  SetOutPath "$INSTDIR"
  File /r "..\publish\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\BWU"
  CreateShortcut "$SMPROGRAMS\BWU\BWU.lnk" "$INSTDIR\BWU.exe"
  CreateShortcut "$SMPROGRAMS\BWU\Uninstall BWU.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "Software\BWU" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME} - ${FULLNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\BWU.exe"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ; Settings and registry backups in %APPDATA%\BWU are intentionally kept so changes can still be reverted.
  SetShellVarContext all
  Delete "$SMPROGRAMS\BWU\BWU.lnk"
  Delete "$SMPROGRAMS\BWU\Uninstall BWU.lnk"
  RMDir "$SMPROGRAMS\BWU"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "BWU"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\BWU"
  DeleteRegKey HKLM "${UNINSTKEY}"
SectionEnd
