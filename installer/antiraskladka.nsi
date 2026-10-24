Unicode true
!include "MUI2.nsh"
Name "Антираскладка"
OutFile "..\dist\Antiraskladka-Setup-1.0.0.exe"
InstallDir "$LOCALAPPDATA\Programs\Antiraskladka"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "1.0.0.0"
VIAddVersionKey /LANG=1049 "ProductName" "Антираскладка"
VIAddVersionKey /LANG=1049 "FileVersion" "1.0.0"
VIAddVersionKey /LANG=1049 "FileDescription" "Установщик Антираскладки"
VIAddVersionKey /LANG=1049 "LegalCopyright" "Иван Воронцов"
!define MUI_FINISHPAGE_RUN "$INSTDIR\Antiraskladka.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить Антираскладку"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Russian"

Section "Install"
  nsExec::Exec 'taskkill /F /IM Antiraskladka.exe'
  Sleep 500
  SetOutPath "$INSTDIR"
  File "..\out\Antiraskladka.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Антираскладка"
  CreateShortcut "$SMPROGRAMS\Антираскладка\Антираскладка.lnk" "$INSTDIR\Antiraskladka.exe"
  CreateShortcut "$SMPROGRAMS\Антираскладка\Удалить.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Antiraskladka" '"$INSTDIR\Antiraskladka.exe"'
  !define UK "Software\Microsoft\Windows\CurrentVersion\Uninstall\Antiraskladka"
  WriteRegStr HKCU "${UK}" "DisplayName" "Антираскладка"
  WriteRegStr HKCU "${UK}" "DisplayVersion" "1.0.0"
  WriteRegStr HKCU "${UK}" "Publisher" "Иван Воронцов"
  WriteRegStr HKCU "${UK}" "DisplayIcon" "$INSTDIR\Antiraskladka.exe"
  WriteRegStr HKCU "${UK}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM Antiraskladka.exe'
  Sleep 500
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Antiraskladka"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Antiraskladka"
  Delete "$INSTDIR\Antiraskladka.exe"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  RMDir /r "$SMPROGRAMS\Антираскладка"
SectionEnd
