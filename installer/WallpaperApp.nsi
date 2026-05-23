; ============================================================================
;  Wallpaper App - NSIS installer script
;  Build:  makensis [/DAPP_VERSION=x.y.z] installer\WallpaperApp.nsi
;  Source: publish\app\  (produced by `dotnet publish -c Release`)
; ============================================================================

Unicode true
SetCompressor /SOLID lzma

!ifndef APP_VERSION
    !define APP_VERSION "1.0.0"
!endif

!define APP_NAME       "Wallpaper App"
!define APP_ID         "WallpaperApp"
!define APP_PUBLISHER  "seanmars"
!define APP_EXE        "WallpaperApp.exe"
!define APP_URL        "https://github.com/seanmars/gnib-wallpaper"
!define APP_REGKEY     "Software\${APP_ID}"
!define UNINST_REGKEY  "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
!define SOURCE_DIR     "..\publish\app"
!define USER_DATA_DIR  "$PROFILE\.gnib-wallpaper"

Name           "${APP_NAME} ${APP_VERSION}"
OutFile        "..\publish\WallpaperApp-Setup-${APP_VERSION}.exe"
InstallDir     "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${APP_REGKEY}" "InstallDir"
RequestExecutionLevel admin
ShowInstDetails show
ShowUnInstDetails show

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey  "ProductName"     "${APP_NAME}"
VIAddVersionKey  "CompanyName"     "${APP_PUBLISHER}"
VIAddVersionKey  "FileVersion"     "${APP_VERSION}"
VIAddVersionKey  "ProductVersion"  "${APP_VERSION}"
VIAddVersionKey  "FileDescription" "${APP_NAME} Installer"
VIAddVersionKey  "LegalCopyright"  "Copyright (c) ${APP_PUBLISHER}"

; ---------------------------------------------------------------------------
;  Modern UI 2
; ---------------------------------------------------------------------------
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON   "setup.ico"
!define MUI_UNICON "setup.ico"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN          "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT     "Launch ${APP_NAME}"
!define MUI_FINISHPAGE_SHOWREADME   ""
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Create desktop shortcut"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopShortcut
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "TradChinese"
!insertmacro MUI_LANGUAGE "English"

; ---------------------------------------------------------------------------
;  Helpers
; ---------------------------------------------------------------------------
Function CreateDesktopShortcut
    CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
FunctionEnd

Function .onInit
    ${IfNot} ${RunningX64}
        MessageBox MB_ICONSTOP "${APP_NAME} requires a 64-bit version of Windows."
        Abort
    ${EndIf}
    SetRegView 64

    ; If a previous version is installed, offer to uninstall it first.
    ReadRegStr $R0 HKLM "${UNINST_REGKEY}" "UninstallString"
    ${If} $R0 != ""
        MessageBox MB_OKCANCEL|MB_ICONQUESTION \
            "${APP_NAME} is already installed.$\n$\nClick OK to remove the previous version, or Cancel to abort the install." \
            IDOK uninst
        Abort
        uninst:
            ClearErrors
            ExecWait '"$R0" /S _?=$INSTDIR'
    ${EndIf}
FunctionEnd

Function un.onInit
    SetRegView 64
FunctionEnd

; ---------------------------------------------------------------------------
;  Install
; ---------------------------------------------------------------------------
Section "Install"
    SetOutPath "$INSTDIR"
    File /r "${SOURCE_DIR}\*.*"

    ; Stop the running app, if any, so we can overwrite the binary.
    nsExec::Exec 'taskkill /F /IM ${APP_EXE} /T'

    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"

    ; --- Registry: install location ---
    WriteRegStr HKLM "${APP_REGKEY}" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "${APP_REGKEY}" "Version"    "${APP_VERSION}"

    ; --- Registry: Programs & Features entry ---
    WriteRegStr   HKLM "${UNINST_REGKEY}" "DisplayName"     "${APP_NAME}"
    WriteRegStr   HKLM "${UNINST_REGKEY}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr   HKLM "${UNINST_REGKEY}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr   HKLM "${UNINST_REGKEY}" "URLInfoAbout"    "${APP_URL}"
    WriteRegStr   HKLM "${UNINST_REGKEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
    WriteRegStr   HKLM "${UNINST_REGKEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr   HKLM "${UNINST_REGKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr   HKLM "${UNINST_REGKEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
    WriteRegDWORD HKLM "${UNINST_REGKEY}" "NoModify" 1
    WriteRegDWORD HKLM "${UNINST_REGKEY}" "NoRepair" 1

    ; --- Estimated install size (KB) ---
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    IntFmt $0 "0x%08X" $0
    WriteRegDWORD HKLM "${UNINST_REGKEY}" "EstimatedSize" "$0"

    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

; ---------------------------------------------------------------------------
;  Uninstall
; ---------------------------------------------------------------------------
Section "Uninstall"
    ; Stop the running app, if any, before deleting files.
    nsExec::Exec 'taskkill /F /IM ${APP_EXE} /T'

    Delete "$DESKTOP\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    RMDir  "$SMPROGRAMS\${APP_NAME}"

    RMDir /r "$INSTDIR"

    DeleteRegKey HKLM "${UNINST_REGKEY}"
    DeleteRegKey HKLM "${APP_REGKEY}"

    ; Ask whether to also remove user preferences (only when run interactively).
    IfSilent +6
    MessageBox MB_YESNO|MB_ICONQUESTION \
        "Also remove user preferences and cached wallpapers?$\n$\n${USER_DATA_DIR}" \
        /SD IDNO IDNO skip_userdata
        RMDir /r "${USER_DATA_DIR}"
    skip_userdata:
SectionEnd
