param([Parameter(Mandatory)][string]$ReleaseDir,[string]$Version='1.0.0-rc.1',[switch]$TestInstall)
$ErrorActionPreference='Stop'
$compiler=Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if(!$compiler) {foreach($base in @(${env:ProgramFiles(x86)},$env:ProgramFiles)) {if(Test-Path "$base/Inno Setup 6/ISCC.exe"){$compiler="$base/Inno Setup 6/ISCC.exe";break}}}
if(!$compiler){throw 'Install Inno Setup 6, or add ISCC.exe to PATH, to build the installer.'}
$arguments=@('/Qp',"/DReleaseDir=$([IO.Path]::GetFullPath($ReleaseDir))","/DAppVersion=$Version")
if($TestInstall){$arguments+='/DTestInstall'}
& $compiler @arguments "$PSScriptRoot/../installer/Wysicraft.iss"
if($LASTEXITCODE){throw 'Installer compilation failed'}
