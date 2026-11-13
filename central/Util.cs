using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace CentralDlb
{
    /// <summary>Endereços e pastas usados em todo o programa.</summary>
    static class Locais
    {
        public const string Dono = "bonanidanilo52-lang";
        public const string Site = "https://dlb-programas.netlify.app";
        public const string Versoes = "https://raw.githubusercontent.com/" + Dono + "/dlb-programas/main/site/versoes.json";

        public static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string Documentos => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static string Usuario => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string Downloads => Path.Combine(Usuario, "Downloads");

        public static string PastaInstalada => Path.Combine(Local, "Programs", "Central DLB");
        public static string ExeInstalado => Path.Combine(PastaInstalada, "Central DLB.exe");
        public static string Dados => Path.Combine(Roaming, "Central DLB");
        public static string Temp => Path.Combine(Path.GetTempPath(), "Central DLB");
    }

    static class Log
    {
        static readonly object trava = new object();
        public static void Info(string t)
        {
            try
            {
                lock (trava)
                {
                    Directory.CreateDirectory(Locais.Dados);
                    var f = Path.Combine(Locais.Dados, "log.txt");
                    if (File.Exists(f) && new FileInfo(f).Length > 300_000) File.Delete(f);
                    File.AppendAllText(f, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "  " + t + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    static class Rede
    {
        static HttpClient cli;
        static HttpClient Cli
        {
            get
            {
                if (cli == null)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // + TLS 1.3
                    cli = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(30) };
                    cli.DefaultRequestHeaders.UserAgent.ParseAdd("CentralDLB/1.0");
                }
                return cli;
            }
        }

        public static async Task<string> Texto(string url)
        {
            // evita cache antigo do GitHub
            var u = url + (url.Contains("?") ? "&" : "?") + "t=" + DateTime.UtcNow.Ticks;
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var r = await Cli.GetAsync(u, cts.Token).ConfigureAwait(false);
                r.EnsureSuccessStatusCode();
                return await r.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Baixa um arquivo avisando a porcentagem (0–100).</summary>
        public static async Task Baixar(string url, string destino, Action<int> pct)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            var tmp = destino + ".parcial";
            using (var r = await Cli.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                r.EnsureSuccessStatusCode();
                long total = r.Content.Headers.ContentLength ?? -1, lido = 0;
                int ultimo = -1;
                using (var ent = await r.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var sai = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    var buf = new byte[1 << 16];
                    int n;
                    while ((n = await ent.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
                    {
                        await sai.WriteAsync(buf, 0, n).ConfigureAwait(false);
                        lido += n;
                        if (total > 0)
                        {
                            int p = (int)(lido * 100 / total);
                            if (p != ultimo) { ultimo = p; pct?.Invoke(p); }
                        }
                    }
                }
            }
            if (File.Exists(destino)) File.Delete(destino);
            File.Move(tmp, destino);
        }
    }

    static class Json
    {
        static JavaScriptSerializer Js => new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static T Ler<T>(string texto) => Js.Deserialize<T>(texto);
        public static string Escrever(object o) => Js.Serialize(o);
        public static Dictionary<string, object> Obj(string texto) => Js.Deserialize<Dictionary<string, object>>(texto);
    }

    static class Versao
    {
        /// <summary>"1.20261113.1318" → comparável.</summary>
        public static Version Ler(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            v = v.Trim().TrimStart('v', 'V');
            return Version.TryParse(v, out var r) ? r : null;
        }

        public static bool Maior(string nova, string atual)
        {
            var a = Ler(nova); var b = Ler(atual);
            if (a == null) return false;
            if (b == null) return true;
            return a > b;
        }

        /// <summary>"1.20261113.1318" → "13/11/2026 13:18". Outros formatos ficam como estão.</summary>
        public static string Bonita(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return "";
            var p = v.Trim().TrimStart('v').Split('.');
            if (p.Length >= 3 && p[1].Length == 8)
            {
                var d = p[1]; var h = p[2].PadLeft(4, '0');
                return $"{d.Substring(6, 2)}/{d.Substring(4, 2)}/{d.Substring(0, 4)} {h.Substring(0, 2)}:{h.Substring(2, 2)}";
            }
            if (p.Length == 1 && p[0].Length == 12) // AAAAMMDDhhmm
            {
                var d = p[0];
                return $"{d.Substring(6, 2)}/{d.Substring(4, 2)}/{d.Substring(0, 4)} {d.Substring(8, 2)}:{d.Substring(10, 2)}";
            }
            return v;
        }

        public static string Atual
        {
            get
            {
                var a = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(Versao).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                return (a?.InformationalVersion ?? "1.0.0").Split('+')[0];
            }
        }
    }

    static class Sistema
    {
        public static void Abrir(string caminhoOuUrl, string args = null)
        {
            try { Process.Start(new ProcessStartInfo(caminhoOuUrl, args ?? "") { UseShellExecute = true }); }
            catch (Exception e) { Log.Info("Abrir " + caminhoOuUrl + ": " + e.Message); }
        }

        public static int Rodar(string exe, string args, bool esperar = true)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true });
                if (!esperar) return 0;
                p.WaitForExit();
                return p.ExitCode;
            }
            catch (Exception e) { Log.Info("Rodar " + exe + " " + args + ": " + e.Message); return -1; }
        }

        public static bool Aberto(string nomeProcesso)
        {
            try { return Process.GetProcessesByName(nomeProcesso).Length > 0; } catch { return false; }
        }

        /// <summary>Cria um atalho .lnk (Área de Trabalho / Menu Iniciar).</summary>
        public static void Atalho(string lnk, string alvo, string args = "", string icone = null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lnk));
                var t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic sh = Activator.CreateInstance(t);
                dynamic a = sh.CreateShortcut(lnk);
                a.TargetPath = alvo;
                a.Arguments = args;
                a.WorkingDirectory = Path.GetDirectoryName(alvo);
                a.IconLocation = (icone ?? alvo) + ",0";
                a.Save();
            }
            catch (Exception e) { Log.Info("Atalho " + lnk + ": " + e.Message); }
        }

        public static string AreaDeTrabalho => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public static string MenuIniciar => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs));

        /// <summary>Procura um programa instalado (lista "Aplicativos" do Windows) pelo começo do nome.</summary>
        public static (string versao, string exe) Instalado(Func<string, bool> nomeConfere)
        {
            foreach (var raiz in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (var k = raiz.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (k == null) continue;
                        foreach (var nome in k.GetSubKeyNames())
                        {
                            using (var s = k.OpenSubKey(nome))
                            {
                                var dn = s?.GetValue("DisplayName") as string;
                                if (dn == null || !nomeConfere(dn)) continue;
                                var ver = s.GetValue("DisplayVersion") as string;
                                var ic = (s.GetValue("DisplayIcon") as string ?? "").Trim('"');
                                var virg = ic.LastIndexOf(',');
                                if (virg > 2) ic = ic.Substring(0, virg);
                                return (ver, File.Exists(ic) ? ic : null);
                            }
                        }
                    }
                }
                catch { }
            }
            return (null, null);
        }

        public static string Tamanho(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0") + " KB";
            if (b < 1024L * 1024 * 1024) return (b / 1024.0 / 1024).ToString("0.0") + " MB";
            return (b / 1024.0 / 1024 / 1024).ToString("0.00") + " GB";
        }

        public static string Quando(DateTime d)
        {
            var hoje = DateTime.Today;
            if (d.Date == hoje) return "hoje às " + d.ToString("HH:mm");
            if (d.Date == hoje.AddDays(-1)) return "ontem às " + d.ToString("HH:mm");
            var dias = (int)(hoje - d.Date).TotalDays;
            return d.ToString("dd/MM/yyyy") + (dias < 60 ? $" (há {dias} dias)" : "");
        }
    }
}
