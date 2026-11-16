using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace CentralDlb
{
    /// <summary>Um dos programas que a Central instala.</summary>
    class Programa
    {
        public string Id, Nome, Descricao, Processo;
        public System.Drawing.Color Cor;

        // situação (preenchida por Verificar)
        public string VersaoInstalada, ExeInstalado, VersaoNova, UrlInstalador;
        public bool PastaAntiga;   // DLB Sistema rodando da pasta de Downloads (antes da Central)
        public string ErroVerificar;

        public bool Instalado => ExeInstalado != null;
        public bool TemAtualizacao => Instalado && VersaoNova != null && (PastaAntiga || Versao.Maior(VersaoNova, VersaoInstalada));

        public Func<Task> Checar;                          // descobre versão instalada e a mais nova
        public Func<Action<string, int>, Task> Instalar;   // baixa e instala (texto, %)
    }

    static class Catalogo
    {
        public static Dictionary<string, object> Versoes;   // site/versoes.json (DLB Sistema e a própria Central)

        public static async Task CarregarVersoes()
        {
            try { Versoes = Json.Obj(await Rede.Texto(Locais.Versoes)); }
            catch (Exception e) { Log.Info("versoes.json: " + e.Message); Versoes = null; }
        }

        public static (string ver, string url) Item(string chave)
        {
            if (Versoes != null && Versoes.TryGetValue(chave, out var o) && o is Dictionary<string, object> d)
                return (d.TryGetValue("ver", out var v) ? v as string : null, d.TryGetValue("url", out var u) ? u as string : null);
            return (null, null);
        }

        static async Task<string> VersaoYml(string repo)
        {
            var yml = await Rede.Texto($"https://github.com/{Locais.Dono}/{repo}/releases/latest/download/latest.yml");
            foreach (var l in yml.Split('\n'))
                if (l.StartsWith("version:")) return l.Substring(8).Trim().Trim('\'', '"');
            return null;
        }

        public static List<Programa> Todos()
        {
            var dlb = new Programa
            {
                Id = "dlb", Nome = "DLB", Processo = "DLB",
                Descricao = "Gestão da empresa: clientes, trabalhos, agenda, financeiro, orçamentos, relatórios e Instagram.",
                Cor = Tema.Laranja,
            };
            dlb.Checar = async () =>
            {
                var (v, exe) = Sistema.Instalado(n => n == "DLB" || n.StartsWith("DLB 1") || n.StartsWith("Studio"));
                dlb.VersaoInstalada = v; dlb.ExeInstalado = exe;
                dlb.UrlInstalador = $"https://github.com/{Locais.Dono}/studio-download/releases/latest/download/Studio-Instalador.exe";
                dlb.VersaoNova = await VersaoYml("studio-download");
            };
            dlb.Instalar = (av) => InstalarNsis(dlb, "Studio-Instalador.exe", av);

            var cri = new Programa
            {
                Id = "criativo", Nome = "DLB Criativo", Processo = "DLB Criativo",
                Descricao = "Quadros de ideias, roteiros e storyboard (estilo Milanote).",
                Cor = Tema.Azul,
            };
            cri.Checar = async () =>
            {
                var (v, exe) = Sistema.Instalado(n => n.StartsWith("DLB Criativo"));
                cri.VersaoInstalada = v; cri.ExeInstalado = exe;
                cri.UrlInstalador = $"https://github.com/{Locais.Dono}/dlb-criativo-download/releases/latest/download/DLB-Criativo-Instalador.exe";
                cri.VersaoNova = await VersaoYml("dlb-criativo-download");
            };
            cri.Instalar = (av) => InstalarNsis(cri, "DLB-Criativo-Instalador.exe", av);

            var sis = new Programa
            {
                Id = "sistema", Nome = "DLB Sistema", Processo = "DLBSistema",
                Descricao = "Monitor do PC: temperatura, FPS no jogo, Modo Jogo, Modo Render e manutenção.",
                Cor = Tema.Creme,
            };
            sis.Checar = () =>
            {
                var exe = Path.Combine(SistemaPasta, "DLBSistema.exe");
                var antigo = Path.Combine(Locais.Downloads, "dlb-sistema", "DLB Sistema", "DLBSistema.exe");
                sis.PastaAntiga = false;
                if (File.Exists(exe))
                {
                    sis.ExeInstalado = exe;
                    var vt = Path.Combine(SistemaPasta, "versao.txt");
                    sis.VersaoInstalada = File.Exists(vt) ? File.ReadAllText(vt).Trim() : null;
                }
                else if (File.Exists(antigo)) { sis.ExeInstalado = antigo; sis.VersaoInstalada = null; sis.PastaAntiga = true; }
                else { sis.ExeInstalado = null; sis.VersaoInstalada = null; }
                var (ver, url) = Item("sistema");
                sis.VersaoNova = ver; sis.UrlInstalador = url;
                return Task.CompletedTask;
            };
            sis.Instalar = (av) => InstalarSistema(sis, av);

            var leg = new Programa
            {
                Id = "legendas", Nome = "DLB Legendas", Processo = "Adobe Premiere Pro",
                Descricao = "Plugin do Premiere: textos e legendas animadas, com legenda automática que ouve o vídeo.",
                Cor = Tema.Laranja,
            };
            leg.Checar = () =>
            {
                var man = Path.Combine(LegendasPasta, "CSXS", "manifest.xml");
                if (File.Exists(man))
                {
                    leg.ExeInstalado = AcharPremiere() ?? LegendasPasta; // "Abrir" abre o Premiere
                    var vt = Path.Combine(LegendasPasta, "versao.txt");
                    leg.VersaoInstalada = File.Exists(vt) ? File.ReadAllText(vt).Trim() : null;
                }
                else { leg.ExeInstalado = null; leg.VersaoInstalada = null; }
                var (ver, url) = Item("legendas");
                leg.VersaoNova = ver; leg.UrlInstalador = url;
                return Task.CompletedTask;
            };
            leg.Instalar = (av) => InstalarLegendas(leg, av);

            var bx = new Programa
            {
                Id = "baixador", Nome = "DLB Baixador", Processo = "Adobe Premiere Pro",
                Descricao = "Plugin do Premiere: baixa vídeo ou só o áudio por link (YouTube, Instagram, Vimeo…) e já importa no projeto.",
                Cor = Tema.Azul,
            };
            bx.Checar = () =>
            {
                var man = Path.Combine(BaixadorPasta, "CSXS", "manifest.xml");
                if (File.Exists(man))
                {
                    bx.ExeInstalado = AcharPremiere() ?? BaixadorPasta; // "Abrir" abre o Premiere
                    var vt = Path.Combine(BaixadorPasta, "versao.txt");
                    bx.VersaoInstalada = File.Exists(vt) ? File.ReadAllText(vt).Trim() : null;
                }
                else { bx.ExeInstalado = null; bx.VersaoInstalada = null; }
                var (ver, url) = Item("baixador");
                bx.VersaoNova = ver; bx.UrlInstalador = url;
                return Task.CompletedTask;
            };
            bx.Instalar = (av) => InstalarBaixador(bx, av);

            return new List<Programa> { dlb, cri, sis, leg, bx };
        }

        // ---------- DLB Baixador (plugin do Premiere) ----------
        // Só o painel: o yt-dlp, o Deno e o ffmpeg o próprio painel baixa na 1ª vez ("Preparar agora").
        public static string BaixadorPasta => Path.Combine(Locais.Roaming, "Adobe", "CEP", "extensions", "com.dlb.baixador");

        static async Task InstalarBaixador(Programa p, Action<string, int> av)
        {
            if (p.UrlInstalador == null) throw new Exception("Sem internet ou o DLB Baixador ainda não foi publicado no site.");
            if (Sistema.Aberto(p.Processo))
                throw new Exception("Feche o Premiere primeiro e tente de novo.");
            var zip = Path.Combine(Locais.Temp, "DLB-Baixador.zip");
            av("Baixando DLB Baixador…", 0);
            await Rede.Baixar(p.UrlInstalador, zip, pc => av($"Baixando DLB Baixador… {pc}%", pc));
            av("Instalando o plugin no Premiere…", -1);
            await Task.Run(() => InstalarPainelCep(zip, BaixadorPasta, p.VersaoNova, "DLB Baixador"));
            Log.Info($"DLB Baixador instalado ({p.VersaoNova}).");
        }

        /// <summary>Extrai um painel CEP (.zip com CSXS\manifest.xml) na pasta de extensões e liga o PlayerDebugMode.</summary>
        static void InstalarPainelCep(string zip, string pasta, string versao, string nome)
        {
            var novo = pasta + ".novo";
            if (Directory.Exists(novo)) Directory.Delete(novo, true);
            ZipFile.ExtractToDirectory(zip, novo);
            var man = Directory.GetFiles(novo, "manifest.xml", SearchOption.AllDirectories).FirstOrDefault()
                      ?? throw new Exception($"O pacote do {nome} veio sem o manifest.xml.");
            var raiz = Path.GetDirectoryName(Path.GetDirectoryName(man)); // ...\CSXS\manifest.xml → pasta do plugin
            Directory.CreateDirectory(Path.GetDirectoryName(pasta));
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
            Directory.Move(raiz, pasta);
            if (Directory.Exists(novo)) Directory.Delete(novo, true);
            File.WriteAllText(Path.Combine(pasta, "versao.txt"), versao ?? "");
            try { File.Delete(zip); } catch { }
            // deixa o Premiere abrir painéis que não vieram da loja da Adobe (só para este usuário)
            for (int n = 9; n <= 13; n++)
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Adobe\CSXS.{n}"))
                    k?.SetValue("PlayerDebugMode", "1");
        }

        // ---------- DLB Legendas (plugin do Premiere + transcrição) ----------
        public static string LegendasPasta => Path.Combine(Locais.Roaming, "Adobe", "CEP", "extensions", "com.dlb.legendas");
        public static string WhisperPasta => Path.Combine(Locais.Documentos, "DLB Legendas", "whisper");

        static string AcharPremiere()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Adobe Premiere Pro.exe"))
                { var v = k?.GetValue("") as string; if (!string.IsNullOrEmpty(v) && File.Exists(v.Trim('"'))) return v.Trim('"'); }
            }
            catch { }
            var bases = new List<string> { Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "Adobe") };
            foreach (var d in DriveInfo.GetDrives())
                try { if (d.DriveType == DriveType.Fixed && d.IsReady) { bases.Add(Path.Combine(d.RootDirectory.FullName, "premiere")); bases.Add(Path.Combine(d.RootDirectory.FullName, "Adobe")); } } catch { }
            foreach (var b in bases)
                try
                {
                    if (!Directory.Exists(b)) continue;
                    var achou = Directory.GetDirectories(b, "Adobe Premiere Pro*").OrderByDescending(x => x)
                        .Select(x => Path.Combine(x, "Adobe Premiere Pro.exe")).FirstOrDefault(File.Exists);
                    if (achou != null) return achou;
                }
                catch { }
            return null;
        }

        static async Task InstalarLegendas(Programa p, Action<string, int> av)
        {
            if (p.UrlInstalador == null) throw new Exception("Sem internet ou o DLB Legendas ainda não foi publicado no site.");
            if (Sistema.Aberto(p.Processo))
                throw new Exception("Feche o Premiere primeiro e tente de novo.");

            // 1) o plugin
            var zip = Path.Combine(Locais.Temp, "DLB-Legendas.zip");
            av("Baixando DLB Legendas…", 0);
            await Rede.Baixar(p.UrlInstalador, zip, pc => av($"Baixando DLB Legendas… {pc}%", pc));
            av("Instalando o plugin no Premiere…", -1);
            await Task.Run(() => InstalarPainelCep(zip, LegendasPasta, p.VersaoNova, "DLB Legendas"));

            // 2) a transcrição (Whisper na placa de vídeo) — só baixa o que faltar
            Directory.CreateDirectory(Path.Combine(WhisperPasta, "bin"));
            var (_, urlW) = Item("legendas_whisper");
            if (urlW != null && !File.Exists(Path.Combine(WhisperPasta, "bin", "whisper-server.exe")))
            {
                var wz = Path.Combine(Locais.Temp, "DLB-Legendas-Transcricao.zip");
                av("Baixando a transcrição automática…", 0);
                await Rede.Baixar(urlW, wz, pc => av($"Baixando a transcrição automática… {pc}%", pc));
                await Task.Run(() =>
                {
                    using (var z = ZipFile.OpenRead(wz))
                        foreach (var e in z.Entries)
                            if (e.Name != "") e.ExtractToFile(Path.Combine(WhisperPasta, "bin", e.Name), true);
                    try { File.Delete(wz); } catch { }
                });
            }
            var modelos = new[] {
                ("ggml-large-v3-turbo-q5_0.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin", "o \"cérebro\" da transcrição (~550 MB)"),
                ("ggml-silero-v5.1.2.bin", "https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v5.1.2.bin", "o filtro de silêncio") };
            foreach (var (arq, url, nome) in modelos)
            {
                var dest = Path.Combine(WhisperPasta, arq);
                if (File.Exists(dest)) continue;
                av($"Baixando {nome}…", 0);
                await Rede.Baixar(url, dest, pc => av($"Baixando {nome}… {pc}%", pc));
            }
            Log.Info($"DLB Legendas instalado ({p.VersaoNova}).");
        }

        public static string SistemaPasta => Path.Combine(Locais.Local, "Programs", "DLB Sistema");

        // ---------- instaladores do DLB e do DLB Criativo (Electron/NSIS) ----------
        static async Task InstalarNsis(Programa p, string arquivo, Action<string, int> av)
        {
            if (p.UrlInstalador == null) throw new Exception("Sem internet ou endereço do instalador indisponível.");
            var dest = Path.Combine(Locais.Temp, arquivo);
            av($"Baixando {p.Nome}…", 0);
            await Rede.Baixar(p.UrlInstalador, dest, pc => av($"Baixando {p.Nome}… {pc}%", pc));
            av($"Instalando {p.Nome}…", -1);
            // /S = instala sem perguntas, no mesmo lugar de antes (por usuário, sem pedir administrador)
            var cod = await Task.Run(() => Sistema.Rodar(dest, "/S"));
            try { File.Delete(dest); } catch { }
            if (cod != 0) throw new Exception($"O instalador do {p.Nome} terminou com erro ({cod}).");
            Log.Info($"{p.Nome} instalado ({p.VersaoNova}).");
        }

        // ---------- DLB Sistema (.zip + .NET 8) ----------
        static bool TemNet8Desktop()
        {
            foreach (var pf in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramW6432") })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                var d = Path.Combine(pf, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
                if (Directory.Exists(d) && Directory.GetDirectories(d, "8.*").Length > 0) return true;
            }
            return false;
        }

        static async Task InstalarSistema(Programa p, Action<string, int> av)
        {
            if (p.UrlInstalador == null) throw new Exception("Sem internet ou o DLB Sistema ainda não foi publicado no site.");
            if (Sistema.Aberto(p.Processo))
                throw new Exception("Feche o DLB Sistema primeiro (clique com o botão direito no ícone perto do relógio › Sair) e tente de novo.");

            if (!TemNet8Desktop())
            {
                var rt = Path.Combine(Locais.Temp, "dotnet8-desktop.exe");
                av("Baixando o .NET 8 (necessário para o DLB Sistema)…", 0);
                await Rede.Baixar("https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe", rt, pc => av($"Baixando o .NET 8… {pc}%", pc));
                av("Instalando o .NET 8 (o Windows vai pedir permissão)…", -1);
                await Task.Run(() =>
                {
                    var pr = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(rt, "/install /quiet /norestart") { UseShellExecute = true, Verb = "runas" });
                    pr.WaitForExit();
                });
                try { File.Delete(rt); } catch { }
                if (!TemNet8Desktop()) throw new Exception("O .NET 8 não foi instalado. Tente de novo e aceite a permissão do Windows.");
            }

            var zip = Path.Combine(Locais.Temp, "DLB-Sistema.zip");
            av("Baixando DLB Sistema…", 0);
            await Rede.Baixar(p.UrlInstalador, zip, pc => av($"Baixando DLB Sistema… {pc}%", pc));
            av("Instalando DLB Sistema…", -1);
            await Task.Run(() =>
            {
                var novo = SistemaPasta + ".novo";
                if (Directory.Exists(novo)) Directory.Delete(novo, true);
                ZipFile.ExtractToDirectory(zip, novo);
                // o .zip pode ter tudo dentro de uma pasta só
                var dentro = Directory.GetFiles(novo, "DLBSistema.exe", SearchOption.AllDirectories).FirstOrDefault()
                             ?? throw new Exception("O pacote do DLB Sistema veio sem o DLBSistema.exe.");
                var raiz = Path.GetDirectoryName(dentro);
                if (Directory.Exists(SistemaPasta)) Directory.Delete(SistemaPasta, true);
                Directory.Move(raiz, SistemaPasta);
                if (Directory.Exists(novo)) Directory.Delete(novo, true);
                File.WriteAllText(Path.Combine(SistemaPasta, "versao.txt"), p.VersaoNova ?? "");
                var exe = Path.Combine(SistemaPasta, "DLBSistema.exe");
                Sistema.Atalho(Path.Combine(Sistema.AreaDeTrabalho, "DLB Sistema.lnk"), exe);
                Sistema.Atalho(Path.Combine(Sistema.MenuIniciar, "DLB Sistema.lnk"), exe);
                try { File.Delete(zip); } catch { }
            });
            Log.Info($"DLB Sistema instalado ({p.VersaoNova}).");
        }
    }
}
