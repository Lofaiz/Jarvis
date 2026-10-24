; Установщик Джарвиса (NSIS 3, Unicode).
; Собирается скриптом Собрать-установщик.ps1, пути передаются через /D.

Unicode true

!include "MUI2.nsh"

!ifndef APP_EXE
    !define APP_EXE "..\dist\Jarvis.exe"
!endif
!ifndef APP_ICON
    !define APP_ICON "..\jarvis.ico"
!endif
!ifndef OUT_FILE
    !define OUT_FILE "..\dist\Jarvis-Setup.exe"
!endif
; необязательные настройки по умолчанию (город и т.п.).
; Путь передаётся из Собрать-установщик.ps1 через /DAPP_CONFIG=...
; Если параметр не задан, config.json в установщик не попадает.

!define APP_NAME      "Джарвис"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "Jarvis"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\Jarvis"

Name "${APP_NAME}"
OutFile "${OUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Jarvis"
InstallDirRegKey HKCU "Software\Jarvis" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
BrandingText "${APP_NAME} ${APP_VERSION}"

VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName"     "${APP_NAME}"
VIAddVersionKey "FileDescription" "Установщик ${APP_NAME}"
VIAddVersionKey "FileVersion"     "${APP_VERSION}.0"
VIAddVersionKey "ProductVersion"  "${APP_VERSION}.0"
VIAddVersionKey "LegalCopyright"  ""

!define MUI_ABORTWARNING
!define MUI_ICON   "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_FINISHPAGE_RUN      "$INSTDIR\Jarvis.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить Джарвиса"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Russian"

Section "Джарвис" SecMain
    SetShellVarContext current

    ; если старая версия запущена — закрываем, иначе файл занят
    nsExec::Exec 'taskkill /IM Jarvis.exe /F'

    SetOutPath "$INSTDIR"
    File "/oname=Jarvis.exe" "${APP_EXE}"

    ; готовые настройки кладём только при первой установке
    !ifdef APP_CONFIG
        IfFileExists "$INSTDIR\config.json" jarvis_config_ready
            File "/oname=config.json" "${APP_CONFIG}"
        jarvis_config_ready:
    !endif

    CreateDirectory "$INSTDIR\model"
    WriteUninstaller "$INSTDIR\uninstall.exe"

    CreateShortCut "$DESKTOP\Джарвис.lnk" "$INSTDIR\Jarvis.exe" "" "$INSTDIR\Jarvis.exe" 0
    CreateDirectory "$SMPROGRAMS\Джарвис"
    CreateShortCut "$SMPROGRAMS\Джарвис\Джарвис.lnk" "$INSTDIR\Jarvis.exe" "" "$INSTDIR\Jarvis.exe" 0
    CreateShortCut "$SMPROGRAMS\Джарвис\Удалить Джарвиса.lnk" "$INSTDIR\uninstall.exe"

    WriteRegStr HKCU "Software\Jarvis" "InstallDir" "$INSTDIR"

    WriteRegStr HKCU "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
    WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\Jarvis.exe"
    WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINST_KEY}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
    WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
    SetShellVarContext current

    nsExec::Exec 'taskkill /IM Jarvis.exe /F'

    Delete "$DESKTOP\Джарвис.lnk"
    Delete "$SMPROGRAMS\Джарвис\Джарвис.lnk"
    Delete "$SMPROGRAMS\Джарвис\Удалить Джарвиса.lnk"
    RMDir "$SMPROGRAMS\Джарвис"

    RMDir /r "$INSTDIR"

    DeleteRegKey HKCU "${UNINST_KEY}"
    DeleteRegKey HKCU "Software\Jarvis"
SectionEnd
