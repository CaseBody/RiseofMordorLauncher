; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

[Setup]
AppName=The Dawnless Days Official Launcher Installer
AppVersion=3.5
DefaultDirName={userappdata}\..\Local\Programs\The Dawnless Days Launcher
DefaultGroupName=The Dawnless Days Launcher
OutputDir=.
OutputBaseFilename=TheDawnlessDaysLauncherSetup
SetupIconFile=RiseofMordorLauncher\Dawnless_Days_square_dark.ico
PrivilegesRequired=admin

[Files]
Source: "Bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\The Dawnless Days Launcher"; Filename: "{app}\AutoUpdater.exe"
; Optional desktop shortcut
Name: "{userdesktop}\The Dawnless Days Launcher"; Filename: "{app}\AutoUpdater.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\AutoUpdater.exe"; Description: "Launch The Dawnless Days Launcher"; Flags: runascurrentuser nowait postinstall skipifsilent
