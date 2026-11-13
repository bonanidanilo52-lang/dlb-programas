# Publica o DLB Sistema: compila a pasta ..\dlb-sistema\src, faz o DLB-Sistema.zip, sobe na release

# "sistema" do repositório dlb-programas e atualiza site/versoes.json (a Central avisa que tem versão nova).

# Uso: powershell -ExecutionPolicy Bypass -File tools\publicar-sistema.ps1

$ErrorActionPreference = 'Stop'

$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')

$raiz = Split-Path $PSScriptRoot -Parent

$src = Join-Path (Split-Path $raiz -Parent) 'dlb-sistema\src'

$repo = 'bonanidanilo52-lang/dlb-programas'

$ver = '1.' + (Get-Date -Format 'yyyyMMdd') + '.' + (Get-Date -Format 'HHmm')



$tmp = Join-Path $env:TEMP 'publicar-dlb-sistema'

if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }

$pasta = Join-Path $tmp 'DLB Sistema'

Write-Host "Compilando o DLB Sistema $ver..."

dotnet publish "$src\DlbSistema.csproj" -c Release -o $pasta --self-contained false | Out-Null

if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou.' }

Remove-Item "$pasta\*.pdb" -ErrorAction SilentlyContinue

$zip = Join-Path $tmp 'DLB-Sistema.zip'

Compress-Archive -Path $pasta -DestinationPath $zip

Write-Host ("Pacote: {0:N1} MB" -f ((Get-Item $zip).Length / 1MB))



gh release view sistema -R $repo *> $null

if ($LASTEXITCODE -ne 0) {

  gh release create sistema -R $repo --title 'DLB Sistema' --notes 'Monitor leve do PC. Instale pela Central DLB (ela também instala o .NET 8 se faltar).' --latest=false

}

Write-Host 'Enviando para o GitHub...'

gh release upload sistema $zip -R $repo --clobber

if ($LASTEXITCODE -ne 0) { throw 'Não consegui enviar o arquivo.' }



$vj = "$raiz\site\versoes.json"

$v = Get-Content $vj -Raw -Encoding UTF8 | ConvertFrom-Json

$v.sistema.ver = $ver

[IO.File]::WriteAllText($vj, ($v | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))

git -C $raiz add -A

git -C $raiz commit -m "DLB Sistema $ver" | Out-Null

git -C $raiz push

Remove-Item $tmp -Recurse -Force

Write-Host "Pronto: DLB Sistema $ver publicado."

