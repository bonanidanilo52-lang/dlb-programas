# Programas DLB — Central DLB + site de downloads (pt-BR)

Dono: Danilo Bonani (não é programador: explicar em linguagem simples, tudo em português).
Criado em 13/11/2026.

## O que tem aqui
- `central/` — **Central DLB** (C# WinForms, **.NET Framework 4.8** de propósito: já vem no Windows 10/11, o .exe tem ~300 KB e roda sem instalar nada).
  - Aberta de qualquer lugar, ela se instala sozinha em `%LOCALAPPDATA%\Programs\Central DLB\Central DLB.exe` (atalhos, entrada em "Aplicativos" com `--desinstalar`) e abre a cópia instalada (`--bemvindo`). `--portatil` não instala.
  - Aba Programas (Programas.cs): DLB e DLB Criativo = baixa o instalador NSIS do "latest" do repositório de download e roda com `/S`; versão nova lida do `latest.yml`, instalada lida do registro (Uninstall). DLB Sistema = baixa `DLB-Sistema.zip`, extrai em `%LOCALAPPDATA%\Programs\DLB Sistema` (+ versao.txt, atalhos); instala o .NET 8 Desktop Runtime se faltar (aka.ms, pede admin).
  - Aba Backup (Backup.cs): destinos = Google Drive (acha "Meu Drive"/"My Drive" em qualquer disco) e pastas/pendrive (guarda o nome do volume para achar se a letra mudar). Em cada destino: `Backup DLB\dados\dados-AAAA-MM-DD.zip` (30 dias, mínimo 3) e `Backup DLB\codigo\codigo-AAAA-MM-DD-hhmm.zip` (10 cópias, só quando algum arquivo de código mudou).
  - Dados que entram: Documentos\Studio-Backup, %APPDATA%\studio (Local Storage/IndexedDB), Documentos\DLB-Criativo-Backup, %APPDATA%\dlb-criativo (Local Storage/IndexedDB), %APPDATA%\DLB Sistema, Documentos\plano-instagram-dlb.json. O zip leva `_mapa.txt` (prefixo|pasta original) usado pelo Restaurar, que renomeia o que existe para `.antes-AAAAMMDD-hhmm` antes de extrair.
  - Código: pastas em Downloads (studio-fonte, studio-computador, dlb-criativo, dlb-sistema, dlb-programas, Portif-lioDanilo), sem node_modules/bin/obj/build/.git e arquivos > 60 MB. **Inclui android/studio.keystore** (chave do APK).
  - Automático: `--auto` pelo Run do usuário (ao entrar no Windows) e tarefa agendada "Central DLB - Backup" de hora em hora; só faz se faltar a cópia do dia em algum destino ligado; balão no canto da tela.
  - Configuração e log: `%APPDATA%\Central DLB\config.json` e `log.txt`.
  - Teste sem instalar: `CentralDLB.exe --captura <pasta>` salva prog.png e bk.png e fecha.
  - Versão: o Windows não aceita 20261113 numa parte da versão do .exe, então a data vai em InformationalVersion (`-p:VersaoDLB=1.AAAAMMDD.hhmm`); `Versao.Atual` lê de lá.
- `site/` — site estático (index.html, icone.png, qr.js = qrcode-generator MIT, versoes.json). Netlify publica a pasta `site` (netlify.toml). Mostra as versões ao vivo (versoes.json, version.json do studio-download e API do GitHub do dlb-criativo-download).
- `tools/` — `gerar-icone.ps1`, `publicar-central.ps1`, `publicar-sistema.ps1`.

## Endereços
- Repositório público: bonanidanilo52-lang/dlb-programas. Releases com tag fixa: `central` (Central-DLB.exe) e `sistema` (DLB-Sistema.zip), substituídas com `--clobber`.
- A Central lê `https://raw.githubusercontent.com/bonanidanilo52-lang/dlb-programas/main/site/versoes.json` ({central:{ver,url}, sistema:{ver,url}}).
- Site: https://dlb-programas.netlify.app (constante `Locais.Site` em Util.cs).

## Publicar
- Central: `powershell -ExecutionPolicy Bypass -File tools\publicar-central.ps1`
- DLB Sistema: `powershell -ExecutionPolicy Bypass -File tools\publicar-sistema.ps1` (compila ..\dlb-sistema\src numa pasta temporária, então pode rodar com o DLB Sistema aberto)
- Os dois fazem commit + push (o push no main atualiza o site). Sempre pedir autorização ao Danilo antes de publicar.
- DLB e DLB Criativo continuam sendo publicados pelos próprios repositórios (studio-download, dlb-criativo-download).
