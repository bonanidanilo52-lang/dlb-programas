# Publica a Central DLB: compila, sobe o Central-DLB.exe na release "central" do dlb-programas
# e atualiza site/versoes.json (a Central instalada avisa sozinha que tem versão nova).
# Uso: powershell -ExecutionPolicy Bypass -File tools\publicar-central.ps1
$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
$raiz = Split-Path $PSScriptRoot -Parent
$repo = 'bonanidanilo52-lang/dlb-programas'
$ver = '1.' + (Get-Date -Format 'yyyyMMdd') + '.' + (Get-Date -Format 'HHmm')

Write-Host "Compilando a Central DLB $ver..."
dotnet build "$raiz\central\CentralDLB.csproj" -c Release -o "$raiz\saida" "-p:VersaoDLB=$ver" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou.' }
$exe = "$raiz\saida\Central-DLB.exe"
Copy-Item "$raiz\saida\CentralDLB.exe" $exe -Force

$ErrorActionPreference = 'Continue'
gh release view central -R $repo 2>&1 | Out-Null
$existe = ($LASTEXITCODE -eq 0)
if (-not $existe) {
  gh release create central -R $repo --title 'Central DLB' --notes 'Instala o DLB, o DLB Criativo e o DLB Sistema e faz o backup. Baixe pelo site.' --latest=false 2>&1 | Out-Host
}
Write-Host 'Enviando para o GitHub...'
gh release upload central $exe -R $repo --clobber 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Não consegui enviar o arquivo.' }
$ErrorActionPreference = 'Stop'

$vj = "$raiz\site\versoes.json"
$v = Get-Content $vj -Raw -Encoding UTF8 | ConvertFrom-Json
$v.central.ver = $ver
[IO.File]::WriteAllText($vj, ($v | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
$ErrorActionPreference = 'Continue'
git -C $raiz add -A 2>&1 | Out-Null
git -C $raiz commit -m "Central DLB $ver" 2>&1 | Out-Null
git -C $raiz push 2>&1 | Out-Host
Write-Host "Pronto: Central DLB $ver publicado."
