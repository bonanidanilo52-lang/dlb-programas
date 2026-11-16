# Publica o DLB Baixador (plugin do Premiere): empacota ..\dlb-baixador\ext como com.dlb.baixador
# e sobe na release "baixador" do dlb-programas. Atualiza site/versoes.json (chave "baixador").
# O yt-dlp, o Deno e o ffmpeg não vão no pacote: o próprio painel baixa na 1ª vez.
# Uso: powershell -ExecutionPolicy Bypass -File tools\publicar-baixador.ps1
$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
$raiz = Split-Path $PSScriptRoot -Parent
$repo = 'bonanidanilo52-lang/dlb-programas'
$ver = '1.' + (Get-Date -Format 'yyyyMMdd') + '.' + (Get-Date -Format 'HHmm')
$ext = Join-Path (Split-Path $raiz -Parent) 'dlb-baixador\ext'
if (-not (Test-Path "$ext\CSXS\manifest.xml")) { throw "Não achei o plugin em $ext" }

$tmp = Join-Path $env:TEMP 'publicar-dlb-baixador'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
$pasta = Join-Path $tmp 'com.dlb.baixador'
New-Item -ItemType Directory -Force $pasta | Out-Null
Get-ChildItem -LiteralPath $ext -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $pasta -Recurse -Force }
$zip = Join-Path $tmp 'DLB-Baixador.zip'
Compress-Archive -Path $pasta -DestinationPath $zip
Write-Host ("Plugin: {0:N2} MB" -f ((Get-Item $zip).Length / 1MB))
if ($env:BX_SO_EMPACOTAR) { Write-Host "Pacote em $zip (não publicado)."; return }

$ErrorActionPreference = 'Continue'
gh release view baixador -R $repo 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
  gh release create baixador -R $repo --title 'DLB Baixador' --notes 'Plugin do Premiere que baixa vídeo ou só o áudio por link e importa no projeto. Instale pela Central DLB. Usa yt-dlp (Unlicense), Deno (MIT) e ffmpeg (GPL), baixados pelo próprio painel.' --latest=false 2>&1 | Out-Host
}
Write-Host 'Enviando para o GitHub...'
gh release upload baixador $zip -R $repo --clobber 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Não consegui enviar o arquivo.' }
$ErrorActionPreference = 'Stop'

$vj = "$raiz\site\versoes.json"
$v = Get-Content $vj -Raw -Encoding UTF8 | ConvertFrom-Json
$v | Add-Member -NotePropertyName baixador -NotePropertyValue ([pscustomobject]@{ ver = $ver; url = "https://github.com/$repo/releases/download/baixador/DLB-Baixador.zip" }) -Force
[IO.File]::WriteAllText($vj, ($v | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
$ErrorActionPreference = 'Continue'
git -C $raiz add -A 2>&1 | Out-Null
git -C $raiz commit -m "DLB Baixador $ver" 2>&1 | Out-Null
git -C $raiz push 2>&1 | Out-Host
Write-Host "Pronto: DLB Baixador $ver publicado."
Remove-Item $tmp -Recurse -Force
