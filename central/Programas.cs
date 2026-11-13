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

            return new List<Programa> { dlb, cri, sis };
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
