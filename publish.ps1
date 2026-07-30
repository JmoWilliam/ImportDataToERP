<#
.SYNOPSIS
    發佈 ImportDataToERP 為「自我包含(self-contained)」部署包。
    客戶端 Windows Server 不需要另外安裝 .NET Runtime 或 IIS Hosting Bundle，
    直接用內建的 Kestrel 伺服器執行（可註冊成 Windows 服務開機自動啟動）。

.PARAMETER OutputDir
    發佈輸出資料夾，預設為此腳本所在目錄下的 publish

.PARAMETER Runtime
    目標執行環境，預設 win-x64 (適用絕大多數 Windows Server)

.PARAMETER Zip
    加上此參數，會把輸出資料夾額外壓縮成 zip，方便傳給客戶端

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Zip
#>
param(
    [string]$OutputDir = (Join-Path $PSScriptRoot "publish"),
    [string]$Runtime = "win-x64",
    [switch]$Zip
)

$ErrorActionPreference = "Stop"
$ProjectPath = Join-Path $PSScriptRoot "ImportDataToERP.csproj"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " ImportDataToERP 自我包含發佈 (客戶端免裝 .NET/IIS)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Write-Host "清除舊的發佈輸出: $OutputDir"
    Remove-Item $OutputDir -Recurse -Force
}

Write-Host "`n[1/4] dotnet publish (self-contained, $Runtime) ..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishReadyToRun=false `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失敗，請檢查上面的錯誤訊息"
}

Write-Host "`n[2/4] 複製資料庫腳本 ..." -ForegroundColor Yellow
$dbScriptDest = Join-Path $OutputDir "DB Script"
New-Item -ItemType Directory -Path $dbScriptDest -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot "database_init.sql") $dbScriptDest -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot "migration_add_missing_columns.sql") $dbScriptDest -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot "Data\DB Script\*.sql") $dbScriptDest -Force -ErrorAction SilentlyContinue

Write-Host "`n[3/4] 移除開發用設定檔 ..." -ForegroundColor Yellow
Remove-Item (Join-Path $OutputDir "appsettings.Development.json") -Force -ErrorAction SilentlyContinue

if ($Zip) {
    Write-Host "`n[4/4] 壓縮輸出 ..." -ForegroundColor Yellow
    $zipPath = "$OutputDir.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath
    Write-Host "已壓縮: $zipPath" -ForegroundColor Green
} else {
    Write-Host "`n[4/4] (略過壓縮，加上 -Zip 參數可自動壓縮成 zip)" -ForegroundColor DarkGray
}

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host " 發佈完成！輸出目錄: $OutputDir" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green

Write-Host @"

部署到客戶端 Windows Server 2019 步驟：

1. 把整個發佈資料夾複製到伺服器，例如 C:\ImportDataToERP
   (裡面已包含 .NET Runtime，伺服器不需要另外安裝任何套件)

2. 編輯 C:\ImportDataToERP\appsettings.json，
   把 ConnectionStrings 改成客戶端實際的 SQL Server 連線資訊：
     - DefaultConnection: 本機用的 ImportDataToERP 資料庫
     - ErpConnection: 客戶端 ERP SQL Server (預設公司資料庫)

3. 建立本機資料庫 (用 sqlcmd 依序執行 "DB Script" 資料夾內的腳本)：
     sqlcmd -S <SQL主機> -U sa -P <密碼> -i "C:\ImportDataToERP\DB Script\database_init.sql"
   之後把資料夾內其他 .sql 檔也依序執行過一次

4. 用系統管理員 PowerShell 把它註冊成 Windows 服務 (開機自動啟動，不需要IIS)：
     sc.exe create ImportDataToERP binPath= "C:\ImportDataToERP\ImportDataToERP.exe --urls http://0.0.0.0:8080" start= auto DisplayName= "ImportDataToERP"
     sc.exe start ImportDataToERP

   (port 8080 可自行更改，記得跟第5步防火牆規則對應)

5. 開放防火牆對外的 port：
     New-NetFirewallRule -DisplayName "ImportDataToERP" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow

6. 瀏覽器開 http://<伺服器IP>:8080 測試登入、公司別下拉選單、拋轉ERP功能

移除服務 (如需要)：
     sc.exe stop ImportDataToERP
     sc.exe delete ImportDataToERP

"@ -ForegroundColor White
