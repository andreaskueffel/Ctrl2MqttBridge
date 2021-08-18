Unicode False
!include MUI2.nsh
!addplugindir .

Name "Ctrl2MqttBridge"
VIProductVersion "2.0.0.0"
VIFileVersion "2.0.0.0"
OutFile "Ctrl2MqttBridgeSetup.exe"
InstallDir C:\Ctrl2MqttBridge

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

Section "CopyFiles"
SetOutPath $INSTDIR
File /r ..\publishwinx86\*
SectionEnd

