$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "C# compiler csc.exe not found under Windows Microsoft.NET directory!"
    Exit 1
}

Write-Host "Using C# compiler: $cscPath" -ForegroundColor Cyan

Write-Host "Compiling IconGenerator.cs..." -ForegroundColor Yellow
& $cscPath /target:exe /out:IconGenerator.exe /reference:System.Drawing.dll,System.dll IconGenerator.cs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compilation of IconGenerator.cs failed!"
    Exit 1
}

Write-Host "Running IconGenerator.exe to generate app.ico..." -ForegroundColor Yellow
& .\IconGenerator.exe app.ico
if ($LASTEXITCODE -ne 0) {
    Write-Error "Generation of app.ico failed!"
    Exit 1
}

Write-Host "Compiling TiwutInstaller.cs..." -ForegroundColor Yellow
$references = "System.Windows.Forms.dll", "System.Drawing.dll", "System.dll", "System.Core.dll", "System.IO.Compression.FileSystem.dll", "System.IO.Compression.dll"
$refArgs = $references | ForEach-Object { "/reference:$_" }

& $cscPath /target:winexe /out:TiwutInstaller.exe /win32icon:app.ico $refArgs TiwutInstaller.cs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Compilation of TiwutInstaller.cs failed!"
    Exit 1
}

Write-Host "Cleaning up temporary build files..." -ForegroundColor Yellow
if (Test-Path .\IconGenerator.exe) { Remove-Item .\IconGenerator.exe }
if (Test-Path .\IconGenerator.pdb) { Remove-Item .\IconGenerator.pdb }

Write-Host "BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "Generated standalone executable: TiwutInstaller.exe" -ForegroundColor Green
$fileInfo = Get-Item .\TiwutInstaller.exe
Write-Host ("Size: {0:N2} KB" -f ($fileInfo.Length / 1KB)) -ForegroundColor Green
