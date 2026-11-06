# ==============================================================================
# LBM (Lenovo Battery Manager) - Pure x64 Assembly Build Script
#
# Author: Marek Wesołowski (WESMAR)
# Description: Compiles resources, assembles pure x64 MASM modules (ml64.exe)
#              and links zero-CRT binary (/NODEFAULTLIB, /ENTRY:mainCRTStartup)
# ==============================================================================

$ErrorActionPreference = "Stop"

# Detect all Visual Studio editions, including standalone Build Tools.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw "vswhere.exe not found: install Visual Studio Build Tools." }
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) { throw "No Visual Studio installation with MSVC C++ tools was found." }

$msvcBase = Get-ChildItem -Path "$vsPath\VC\Tools\MSVC" -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $msvcBase) { throw "MSVC installation directory missing in $vsPath" }
$sdkBase  = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" | Where-Object { $_.Name -like "10.*" } | Sort-Object Name -Descending | Select-Object -First 1

$ml64Exe = "$($msvcBase.FullName)\bin\Hostx64\x64\ml64.exe"
$linkExe = "$($msvcBase.FullName)\bin\Hostx64\x64\link.exe"
$rcExe   = "$($sdkBase.FullName)\x64\rc.exe"

$sdkInc  = "C:\Program Files (x86)\Windows Kits\10\Include\$($sdkBase.Name)\um"
$sdkShared = "C:\Program Files (x86)\Windows Kits\10\Include\$($sdkBase.Name)\shared"
$sdkLib  = "C:\Program Files (x86)\Windows Kits\10\Lib\$($sdkBase.Name)\um\x64"
$msvcLib = "$($msvcBase.FullName)\lib\x64"

Write-Host "[*] Visual Studio MSVC path: $vsPath"

if (-not (Test-Path $ml64Exe)) { throw "ml64.exe not found" }
if (-not (Test-Path $linkExe)) { throw "link.exe not found" }

$rootDir = $PSScriptRoot
$asmDir = Join-Path $rootDir "src"
$binDir = Join-Path $rootDir "bin"

if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Force -Path $binDir | Out-Null
}

# Intermediate files (.obj, .res) are written to a temporary directory under
# %TEMP%, which is removed after the build, so bin/ holds only lbm.exe
$tempDir = Join-Path $env:TEMP "lbm_build_$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
Write-Host "[*] Temporary directory: $tempDir"

try {
    $rcFile = Join-Path $rootDir "res\resource.rc"

    Write-Host "[*] Compiling the XML manifest and Win32 resources (rc.exe)..."
    Set-Location $asmDir
    & $rcExe /nologo /i "$sdkInc" /i "$sdkShared" /fo "$tempDir\resource.res" $rcFile
    if ($LASTEXITCODE -ne 0) {
        throw "Resource compilation failed"
    }

    $asmFiles = @("main.asm", "lbm_api.asm", "window.asm", "uac.asm", "nudge.asm")
    Write-Host "[*] Assembling x64 MASM modules (ml64.exe)..."

    foreach ($asmFile in $asmFiles) {
        Write-Host "  -> Assembling: $asmFile"
        & $ml64Exe /c /nologo /Cp /W3 /Fo"$tempDir\$([IO.Path]::GetFileNameWithoutExtension($asmFile)).obj" $asmFile
        if ($LASTEXITCODE -ne 0) {
            throw "Assembly failed for $asmFile"
        }
    }

    Write-Host "[*] Linking the pure x64 Release binary (no CRT)..."
    $exeFile = Join-Path $binDir "lbm.exe"

    $linkArgs = @(
        "/NOLOGO",
        "/RELEASE",
        "/INCREMENTAL:NO",
        "/OPT:REF",
        "/OPT:ICF",
        "/DYNAMICBASE",
        "/HIGHENTROPYVA",
        "/NXCOMPAT",
        "/SUBSYSTEM:WINDOWS",
        "/ENTRY:mainCRTStartup",
        "/NODEFAULTLIB",
        "/MACHINE:X64",
        "/LIBPATH:$sdkLib",
        "/LIBPATH:$msvcLib",
        "/OUT:$exeFile",
        (Join-Path $tempDir "resource.res"),
        (Join-Path $tempDir "main.obj"),
        (Join-Path $tempDir "lbm_api.obj"),
        (Join-Path $tempDir "window.obj"),
        (Join-Path $tempDir "uac.obj"),
        (Join-Path $tempDir "nudge.obj"),
        "kernel32.lib",
        "user32.lib",
        "gdi32.lib",
        "advapi32.lib",
        "shell32.lib",
        "comctl32.lib",
        "dwmapi.lib"
    )

    & $linkExe $linkArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Linking failed"
    }

    Set-Location $rootDir

    Write-Host "[+] Release build completed successfully."
    Write-Host "    Output: $exeFile"
}
finally {
    # Remove intermediate files regardless of the build result
    Set-Location $rootDir
    if (Test-Path $tempDir) {
        Write-Host "[*] Cleaning up temporary files..."
        Remove-Item -Recurse -Force -Path $tempDir -ErrorAction SilentlyContinue
    }
}
