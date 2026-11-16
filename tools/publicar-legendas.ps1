# Publica o DLB Legendas (plugin do Premiere): empacota ..\dlb-legendas\ext como com.dlb.legendas,
# junta os programas da transcrição (whisper-cli/whisper-server com Vulkan, do repositório dlb-whisper)
# e sobe os dois na release "legendas" do dlb-programas. Atualiza site/versoes.json.
# O "cérebro" (modelo) e o filtro de silêncio a Central baixa direto do Hugging Face.
# Uso: powershell -ExecutionPolicy Bypass -File tools\publicar-legendas.ps1
$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
$raiz = Split-Path $PSScriptRoot -Parent
$repo = 'bonanidanilo52-lang/dlb-programas'
$ver = '1.' + (Get-Date -Format 'yyyyMMdd') + '.' + (Get-Date -Format 'HHmm')
$ext = Join-Path (Split-Path $raiz -Parent) 'dlb-legendas\ext'
$bin = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'DLB Legendas\whisper\bin'
if (-not (Test-Path "$ext\CSXS\manifest.xml")) { throw "Não achei o plugin em $ext" }

$tmp = Join-Path $env:TEMP 'publicar-dlb-legendas'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
$pasta = Join-Path $tmp 'com.dlb.legendas'
New-Item -ItemType Directory -Force $pasta | Out-Null
Get-ChildItem -LiteralPath $ext -Force | Where-Object { $_.Name -ne 'dev.html' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $pasta -Recurse -Force }
$zip = Join-Path $tmp 'DLB-Legendas.zip'
Compress-Archive -Path $pasta -DestinationPath $zip
Write-Host ("Plugin: {0:N1} MB" -f ((Get-Item $zip).Length / 1MB))

$wzip = Join-Path $tmp 'DLB-Legendas-Transcricao.zip'
$arqs = @('whisper-cli.exe', 'whisper-server.exe', 'LICENSE-whisper.cpp.txt') | ForEach-Object { Join-Path $bin $_ } | Where-Object { Test-Path $_ }
if ($arqs.Count -lt 2) { throw "Não achei os programas da transcrição em $bin" }
Compress-Archive -Path $arqs -DestinationPath $wzip
Write-Host ("Transcrição: {0:N1} MB" -f ((Get-Item $wzip).Length / 1MB))

$ErrorActionPreference = 'Continue'
gh release view legendas -R $repo 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
  gh release create legendas -R $repo --title 'DLB Legendas' --notes 'Plugin do Premiere com legendas animadas e legenda automática. Instale pela Central DLB. A transcrição usa o whisper.cpp (MIT) compilado com Vulkan.' --latest=false 2>&1 | Out-Host
}
Write-Host 'Enviando para o GitHub...'
gh release upload legendas $zip $wzip -R $repo --clobber 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Não consegui enviar os arquivos.' }
$ErrorActionPreference = 'Stop'

$vj = "$raiz\site\versoes.json"
$v = Get-Content $vj -Raw -Encoding UTF8 | ConvertFrom-Json
$base = "https://github.com/$repo/releases/download/legendas"
$v | Add-Member -NotePropertyName legendas -NotePropertyValue ([pscustomobject]@{ ver = $ver; url = "$base/DLB-Legendas.zip" }) -Force
$v | Add-Member -NotePropertyName legendas_whisper -NotePropertyValue ([pscustomobject]@{ ver = $ver; url = "$base/DLB-Legendas-Transcricao.zip" }) -Force
[IO.File]::WriteAllText($vj, ($v | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
$ErrorActionPreference = 'Continue'
git -C $raiz add -A 2>&1 | Out-Null
git -C $raiz commit -m "DLB Legendas $ver" 2>&1 | Out-Null
git -C $raiz push 2>&1 | Out-Host
Write-Host "Pronto: DLB Legendas $ver publicado."
Remove-Item $tmp -Recurse -Force
