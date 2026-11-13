# Publica a Central DLB: compila, sobe o Central-DLB.exe na release "central" do repositório

# dlb-programas e atualiza site/versoes.json (a Central instalada avisa sozinha que tem versão nova).

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



gh release view central -R $repo *> $null

if ($LASTEXITCODE -ne 0) {

  gh release create central -R $repo --title 'Central DLB' --notes 'Instala o DLB, o DLB Criativo e o DLB Sistema e faz o backup. Baixe pelo site.' --latest=false

}

Write-Host 'Enviando para o GitHub...'

gh release upload central $exe -R $repo --clobber

if ($LASTEXITCODE -ne 0) { throw 'Não consegui enviar o arquivo.' }



$vj = "$raiz\site\versoes.json"

$v = Get-Content $vj -Raw -Encoding UTF8 | ConvertFrom-Json

$v.central.ver = $ver

[IO.File]::WriteAllText($vj, ($v | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))

git -C $raiz add -A

git -C $raiz commit -m "Central DLB $ver" | Out-Null

git -C $raiz push

Write-Host "Pronto: Central DLB $ver publicada."

