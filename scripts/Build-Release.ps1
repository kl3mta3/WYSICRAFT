param([string]$JavaHome=$env:JAVA_HOME,[switch]$Installer)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $env:JAVA_HOME=$JavaHome
    & ./wysicraft-runtime/gradlew.bat -p wysicraft-runtime jar
    if($LASTEXITCODE){throw 'Runtime build failed'}
    $runtimeVersion=[regex]::Match((Get-Content wysicraft-runtime/build.gradle -Raw),"version = '([^']+)'").Groups[1].Value
    $version=[regex]::Match((Get-Content src/Wysicraft.Designer/Wysicraft.Designer.csproj -Raw),'<Version>([^<]+)</Version>').Groups[1].Value
    if(!$version){$version=$runtimeVersion}
    $release=Join-Path $repo "artifacts/WYSICRAFT-$version"
    & dotnet publish src/Wysicraft.Designer -c Release -r win-x64 --self-contained true -o "$release/Designer"
    if($LASTEXITCODE){throw 'Designer build failed'}
    New-Item -ItemType Directory -Force "$release/Runtime","$release/TestEnvironment","$release/Docs"|Out-Null
    Copy-Item "wysicraft-runtime/build/libs/wysicraft-$runtimeVersion.jar" "$release/Runtime/" -Force
    foreach($name in @('gradlew','gradlew.bat','build.gradle','settings.gradle','gradle.properties','gradle','src')) {
        Copy-Item "wysicraft-runtime/$name" "$release/TestEnvironment/" -Recurse -Force
    }
    Copy-Item docs/* "$release/Docs/" -Recurse -Force
    Copy-Item README.md,LICENSE $release -Force
    & "$PSScriptRoot/Collect-Notices.ps1" -ReleaseDir $release
    Compress-Archive "$release/*" "artifacts/WYSICRAFT-$version-win-x64.zip" -Force
    if($Installer){ & "$PSScriptRoot/Build-Installer.ps1" -ReleaseDir $release -Version $version }
    Write-Output "Release: $release (editor tests and exports use Runtime/wysicraft-$runtimeVersion.jar)"
} finally {Pop-Location}

