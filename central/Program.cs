using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CentralDlb
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var a = args.Select(x => x.ToLowerInvariant()).ToArray();

            // backup silencioso (Windows ligando / de hora em hora)
            if (a.Contains("--auto")) return BackupSilencioso();

            if (a.Contains("--desinstalar"))
            {
                if (MessageBox.Show("Desinstalar a Central DLB?\n\nOs programas (DLB, DLB Criativo, DLB Sistema) e os seus backups continuam onde estão.",
                        "Central DLB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Instalacao.Desinstalar();
                return 0;
            }

            // --captura <pasta>: salva imagens das telas para conferência (testes)
            var iCap = Array.IndexOf(a, "--captura");
            if (iCap >= 0 && iCap + 1 < args.Length) { Janela.Capturar(args[iCap + 1]); return 0; }

            // atualização: espera a versão antiga fechar
            var iAt = Array.IndexOf(a, "--atualizar");
            if (iAt >= 0 && iAt + 1 < args.Length && int.TryParse(args[iAt + 1], out var pid))
                try { Process.GetProcessById(pid).WaitForExit(15000); } catch { }

            // aberta de fora (Downloads, site…): instala e abre a cópia instalada
            if (!Instalacao.RodandoInstalada && !a.Contains("--portatil"))
            {
                try
                {
                    Instalacao.Instalar();
                    var cfg = Config.Carregar();
                    if (cfg.Auto) Instalacao.BackupAutomatico(true);
                    Process.Start(new ProcessStartInfo(Locais.ExeInstalado, iAt >= 0 ? "--atualizada" : "--bemvindo") { UseShellExecute = true });
                    return 0;
                }
                catch (Exception e)
                {
                    Log.Info("instalar: " + e);
                    MessageBox.Show("Não consegui instalar a Central neste PC:\n" + e.Message + "\n\nEla vai abrir assim mesmo.", "Central DLB");
                }
            }

            using (var mtx = new Mutex(true, "CentralDLB-janela", out bool sozinho))
            {
                if (!sozinho)
                {
                    MessageBox.Show("A Central DLB já está aberta.", "Central DLB");
                    return 0;
                }
                Application.Run(new Janela(a.Contains("--bemvindo"), a.Contains("--atualizada")));
            }
            return 0;
        }

        static int BackupSilencioso()
        {
            using (var mtx = new Mutex(true, "CentralDLB-backup", out bool sozinho))
            {
                if (!sozinho) return 0;
                var c = Config.Carregar();
                if (!c.Auto || c.Destinos.Count == 0) return 0;
                if (c.Destinos.All(d => Backup.Raiz(d) == null || Backup.FeitoHoje(d)))
                {
                    // nada a fazer; avisa só se faz tempo que nenhum backup acontece
                    if (DateTime.TryParse(c.UltimoBackup, out var ult) && (DateTime.Now - ult).TotalDays >= 3 && DateTime.Now.Hour >= 9 && DateTime.Now.Hour < 22
                        && !DateTime.Today.ToString("yyyy-MM-dd").Equals(LerAvisoDia()))
                    {
                        GravarAvisoDia();
                        Balao("Backup atrasado", $"O último backup foi {Sistema.Quando(ult)}. Ligue o pendrive ou o Google Drive.", ToolTipIcon.Warning);
                    }
                    return 0;
                }
                Thread.Sleep(TimeSpan.FromSeconds(20)); // deixa o Windows terminar de ligar
                try
                {
                    var r = Backup.Fazer(c, _ => { }, soSeFaltar: true);
                    if (r.Ok) Balao("Backup feito", "Seus dados estão guardados em: " + string.Join(", ", r.Feitos) + ".", ToolTipIcon.Info);
                    else if (r.Avisos.Count > 0) Balao("Backup com problema", string.Join("\n", r.Avisos), ToolTipIcon.Warning);
                }
                catch (Exception e)
                {
                    Log.Info("auto: " + e);
                    c.UltimoErro = e.Message; c.Salvar();
                    Balao("Backup não foi feito", e.Message, ToolTipIcon.Error);
                }
            }
            return 0;
        }

        static string ArqAviso => Path.Combine(Locais.Dados, "aviso.txt");
        static string LerAvisoDia() { try { return File.ReadAllText(ArqAviso).Trim(); } catch { return ""; } }
        static void GravarAvisoDia() { try { File.WriteAllText(ArqAviso, DateTime.Today.ToString("yyyy-MM-dd")); } catch { } }

        /// <summary>Mostra um aviso no canto da tela por alguns segundos e fecha.</summary>
        static void Balao(string titulo, string texto, ToolTipIcon ic)
        {
            using (var ni = new NotifyIcon { Icon = Tema.Icone, Visible = true, Text = "Central DLB" })
            {
                ni.BalloonTipClicked += (s, e) => Sistema.Abrir(Locais.ExeInstalado);
                ni.ShowBalloonTip(8000, titulo, texto, ic);
                var fim = DateTime.Now.AddSeconds(9);
                while (DateTime.Now < fim) { Application.DoEvents(); Thread.Sleep(100); }
                ni.Visible = false;
            }
        }
    }
}
