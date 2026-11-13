using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CentralDlb
{
    class Janela : Form
    {
        readonly Config cfg = Config.Carregar();
        readonly List<Programa> progs = Catalogo.Todos();
        readonly Panel rolagem = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Fundo };
        readonly FlowLayoutPanel lista = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Tema.Fundo, Location = Point.Empty };
        readonly Panel faixaAviso = new Panel { Dock = DockStyle.Top, Visible = false, BackColor = Color.FromArgb(0x3A, 0x26, 0x14) };
        readonly Label txAviso = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Tema.Texto, TextAlign = ContentAlignment.MiddleLeft };
        readonly Button btAviso;
        readonly ProgressBar barra = new ProgressBar { Dock = DockStyle.Right, Visible = false, Style = ProgressBarStyle.Continuous };
        readonly Label txStatus = new Label { Dock = DockStyle.Fill, ForeColor = Tema.Apagado, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        readonly Button abaProg, abaBk;
        string aba = "prog";
        bool ocupado;
        readonly bool bemVindo, atualizada;
        string capturaPasta;

        public Janela(bool bemVindo = false, bool atualizada = false)
        {
            this.bemVindo = bemVindo; this.atualizada = atualizada;
            using (var g = CreateGraphics()) Tema.Escala = g.DpiX / 96f;
            Text = "Central DLB"; Icon = Tema.Icone; BackColor = Tema.Fundo; ForeColor = Tema.Texto; Font = Tema.Fonte(10);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Tema.D(900), Tema.D(720)); MinimumSize = new Size(Tema.D(560), Tema.D(520));
            DoubleBuffered = true;

            // ---- topo ----
            var topo = new Panel { Dock = DockStyle.Top, Height = Tema.D(76), BackColor = Tema.Fundo, Padding = new Padding(Tema.D(22), Tema.D(14), Tema.D(22), 0) };
            var logo = new PictureBox { Image = new Icon(Tema.Icone, Tema.D(48), Tema.D(48)).ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(Tema.D(48), Tema.D(48)), Location = new Point(Tema.D(22), Tema.D(14)) };
            var titulo = Tema.Rotulo("Central DLB", 17, Tema.Texto, FontStyle.Bold); titulo.Location = new Point(Tema.D(80), Tema.D(10));
            var sub = Tema.Rotulo("Instala, atualiza e guarda o backup dos seus programas", 9.5f, Tema.Apagado); sub.Location = new Point(Tema.D(82), Tema.D(46));
            var ver = Tema.Rotulo("versão " + Versao.Bonita(Versao.Atual), 8.5f, Tema.Apagado); ver.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            topo.Controls.AddRange(new Control[] { logo, titulo, sub, ver });
            topo.Resize += (s, e) => ver.Location = new Point(topo.Width - ver.Width - Tema.D(22), Tema.D(16));

            // ---- abas ----
            var abas = new FlowLayoutPanel { Dock = DockStyle.Top, Height = Tema.D(50), BackColor = Tema.Fundo, Padding = new Padding(Tema.D(20), Tema.D(6), 0, 0) };
            abaProg = Tema.Botao("📦  Programas"); abaBk = Tema.Botao("💾  Backup");
            abaProg.Click += (s, e) => Trocar("prog"); abaBk.Click += (s, e) => Trocar("bk");
            abas.Controls.AddRange(new Control[] { abaProg, abaBk });

            // ---- faixa de aviso (atualização da Central / boas-vindas) ----
            faixaAviso.Height = Tema.D(46); faixaAviso.Padding = new Padding(Tema.D(22), 0, Tema.D(14), 0);
            btAviso = Tema.Botao("Atualizar", true); btAviso.Dock = DockStyle.Right; btAviso.Margin = Padding.Empty;
            faixaAviso.Controls.Add(txAviso); faixaAviso.Controls.Add(btAviso);

            // ---- rodapé ----
            var rodape = new Panel { Dock = DockStyle.Bottom, Height = Tema.D(40), BackColor = Color.FromArgb(0x12, 0x12, 0x14), Padding = new Padding(Tema.D(22), Tema.D(8), Tema.D(22), Tema.D(8)) };
            barra.Width = Tema.D(220);
            rodape.Controls.Add(txStatus); rodape.Controls.Add(barra);

            lista.Location = new Point(Tema.D(22), Tema.D(8));
            lista.Padding = new Padding(0, 0, 0, Tema.D(16));
            rolagem.Controls.Add(lista);
            rolagem.Resize += (s, e) => AjustarLargura();

            Controls.Add(rolagem); Controls.Add(faixaAviso); Controls.Add(abas); Controls.Add(topo); Controls.Add(rodape);
            Trocar("prog");
            Shown += async (s, e) => await Iniciar();
        }

        public static void Capturar(string pasta)
        {
            var j = new Janela { capturaPasta = pasta };
            Application.Run(j);
        }

        // ================= geral =================
        void Status(string t, int pct = -2)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Status(t, pct))); return; }
            txStatus.Text = t;
            if (pct == -2) { barra.Visible = false; }
            else if (pct < 0) { barra.Visible = true; barra.Style = ProgressBarStyle.Marquee; }
            else { barra.Visible = true; barra.Style = ProgressBarStyle.Continuous; barra.Value = Math.Max(0, Math.Min(100, pct)); }
        }

        void Trocar(string qual)
        {
            aba = qual;
            foreach (var (b, id) in new[] { (abaProg, "prog"), (abaBk, "bk") })
            {
                var ativo = id == aba;
                b.BackColor = ativo ? Tema.Cartao : Tema.Fundo;
                b.ForeColor = ativo ? Tema.Laranja : Tema.Apagado;
                b.Font = Tema.Fonte(11, ativo ? FontStyle.Bold : FontStyle.Regular);
            }
            Montar();
        }

        void Montar()
        {
            lista.SuspendLayout();
            foreach (Control c in lista.Controls.Cast<Control>().ToList()) c.Dispose();
            lista.Controls.Clear();
            if (aba == "prog") MontarProgramas(); else MontarBackup();
            lista.ResumeLayout();
            rolagem.AutoScrollPosition = Point.Empty;
            lista.Location = new Point(Tema.D(22), Tema.D(8));
            AjustarLargura();
        }

        void AjustarLargura()
        {
            // reserva sempre o lugar da barra de rolagem, para todos os cartões ficarem da mesma largura
            var w = rolagem.Width - Tema.D(44) - SystemInformation.VerticalScrollBarWidth;
            if (w < 100) return;
            foreach (Control c in lista.Controls)
                if (c is Cartao k) k.Ajustar(w);
                else if (c is Label l) l.MaximumSize = new Size(w, 0);
        }

        async Task Iniciar()
        {
            if (bemVindo || atualizada)
                MostrarAviso(atualizada ? "✓ A Central DLB foi atualizada." : "✓ Central DLB instalada. Ela fica na Área de Trabalho e no Menu Iniciar.", null, null);
            await Verificar();
            if (capturaPasta != null) { await SalvarCapturas(); Close(); }
        }

        void MostrarAviso(string texto, string botao, Action acao)
        {
            txAviso.Text = texto;
            btAviso.Visible = botao != null;
            if (botao != null) { btAviso.Text = botao; btAviso.Click -= cliqueAviso; cliqueAviso = (s, e) => acao(); btAviso.Click += cliqueAviso; }
            faixaAviso.Visible = true;
        }
        EventHandler cliqueAviso;

        async Task Verificar()
        {
            Status("Procurando versões novas…", -1);
            await Catalogo.CarregarVersoes();
            foreach (var p in progs)
            {
                try { p.ErroVerificar = null; await p.Checar(); }
                catch (Exception e) { p.ErroVerificar = "Sem internet para ver a versão nova."; Log.Info("checar " + p.Nome + ": " + e.Message); }
            }
            var (vc, uc) = Catalogo.Item("central");
            if (vc != null && uc != null && Versao.Maior(vc, Versao.Atual))
                MostrarAviso($"Tem uma Central DLB nova ({Versao.Bonita(vc)}).", "Atualizar a Central", async () => await AtualizarCentral(uc));
            var falta = progs.Count(p => !p.Instalado || p.TemAtualizacao);
            Status(Catalogo.Versoes == null && progs.All(p => p.ErroVerificar != null) ? "Sem internet: não deu para ver as versões novas." :
                   falta == 0 ? "Tudo instalado e em dia ✓" : $"{falta} programa(s) para instalar ou atualizar.");
            if (aba == "prog") Montar();
        }

        async Task AtualizarCentral(string url)
        {
            if (ocupado) return;
            ocupado = true;
            try
            {
                var dest = Path.Combine(Locais.Temp, "Central-DLB-nova.exe");
                await Rede.Baixar(url, dest, pc => Status($"Baixando a Central nova… {pc}%", pc));
                Process.Start(new ProcessStartInfo(dest, "--atualizar " + Process.GetCurrentProcess().Id) { UseShellExecute = true });
                Close();
            }
            catch (Exception e) { Status("Não deu para atualizar: " + e.Message); }
            finally { ocupado = false; }
        }

        static FlowLayoutPanel Linha() => new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Tema.Cartao, Margin = new Padding(0, Tema.D(4), 0, 0), Padding = Padding.Empty,
        };

        static Label Quebra(string t, float pt = 10, Color? cor = null, FontStyle st = FontStyle.Regular)
        {
            var l = Tema.Rotulo(t, pt, cor, st); l.Tag = "quebra"; return l;
        }

        Label Titulo(string t)
        {
            var l = Tema.Rotulo(t, 12.5f, Tema.Texto, FontStyle.Bold);
            l.Margin = new Padding(0, Tema.D(6), 0, Tema.D(8));
            return l;
        }

        // ================= PROGRAMAS =================
        void MontarProgramas()
        {
            foreach (var p in progs) lista.Controls.Add(CartaoPrograma(p));

            var fim = new Cartao();
            var falta = progs.Where(p => !p.Instalado || p.TemAtualizacao).ToList();
            fim.Corpo.Controls.Add(Quebra(falta.Count == 0 ? "Está tudo instalado e na versão mais nova." :
                "Faltando: " + string.Join(", ", falta.Select(p => p.Nome + (p.Instalado ? " (atualizar)" : ""))), 10.5f));
            var l = Linha();
            var tudo = Tema.Botao("⬇  Instalar o que falta", true); tudo.Enabled = falta.Count > 0;
            tudo.Click += async (s, e) => { foreach (var p in falta) if (!await InstalarUm(p)) break; };
            var de = Tema.Botao("↻  Verificar de novo"); de.Click += async (s, e) => { if (!ocupado) await Verificar(); };
            var site = Tema.Botao("🌐  Site e app do celular"); site.Click += (s, e) => Sistema.Abrir(Locais.Site);
            l.Controls.AddRange(new Control[] { tudo, de, site });
            fim.Corpo.Controls.Add(l);
            lista.Controls.Add(fim);
        }

        Cartao CartaoPrograma(Programa p)
        {
            var k = new Cartao { Faixa = p.Cor };
            var cab = Linha(); cab.Margin = Padding.Empty;
            var nome = Tema.Rotulo(p.Nome, 14, Tema.Texto, FontStyle.Bold);
            cab.Controls.Add(nome);
            string chip; Color corChip;
            if (!p.Instalado) { chip = "não instalado"; corChip = Tema.Apagado; }
            else if (p.TemAtualizacao) { chip = "atualização disponível"; corChip = Tema.Laranja; }
            else if (p.VersaoNova == null) { chip = "instalado"; corChip = Tema.Verde; }
            else { chip = "em dia ✓"; corChip = Tema.Verde; }
            var c = Tema.Rotulo(chip, 9, corChip, FontStyle.Bold); c.Margin = new Padding(Tema.D(6), Tema.D(7), 0, 0);
            cab.Controls.Add(c);
            k.Corpo.Controls.Add(cab);
            k.Corpo.Controls.Add(Quebra(p.Descricao, 10, Tema.Apagado));

            var info = new List<string>();
            if (p.Instalado && p.VersaoInstalada != null) info.Add("Instalada: " + Versao.Bonita(p.VersaoInstalada));
            if (p.PastaAntiga) info.Add("Rodando da pasta Downloads (cópia de teste). Instale para receber atualizações.");
            if (p.VersaoNova != null) info.Add("Mais nova: " + Versao.Bonita(p.VersaoNova));
            else if (p.ErroVerificar != null) info.Add(p.ErroVerificar);
            else if (p.Id == "sistema") info.Add("Ainda não publicado no site.");
            if (info.Count > 0) k.Corpo.Controls.Add(Quebra(string.Join("   ·   ", info), 9, Tema.Apagado));

            var l = Linha(); l.Margin = new Padding(0, Tema.D(8), 0, 0);
            if (!p.Instalado || p.TemAtualizacao)
            {
                var b = Tema.Botao(p.Instalado ? "⬆  Atualizar" : "⬇  Instalar", true);
                b.Enabled = p.UrlInstalador != null && (p.VersaoNova != null || p.Id != "sistema");
                b.Click += async (s, e) => await InstalarUm(p);
                l.Controls.Add(b);
            }
            if (p.Instalado)
            {
                var a = Tema.Botao("▶  Abrir"); a.Click += (s, e) => Sistema.Abrir(p.ExeInstalado);
                l.Controls.Add(a);
            }
            k.Corpo.Controls.Add(l);
            return k;
        }

        async Task<bool> InstalarUm(Programa p)
        {
            if (ocupado) return false;
            if (p.Id != "sistema" && Sistema.Aberto(p.Processo))
            {
                if (MessageBox.Show($"O {p.Nome} está aberto. Ele será fechado para instalar a versão nova.\n\nContinuar?", "Central DLB",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return false;
            }
            ocupado = true;
            var antiga = p.PastaAntiga;
            try
            {
                await p.Instalar((t, pc) => Status(t, pc));
                await p.Checar();
                Status($"✓ {p.Nome} instalado.");
                if (antiga)
                    MessageBox.Show("DLB Sistema instalado no lugar certo, com atalho na Área de Trabalho.\n\n" +
                        "Se ele estava configurado para \"Iniciar com o Windows\", abra o DLB Sistema › Ajustes e ligue essa opção de novo " +
                        "(para apontar para o lugar novo).\n\nA pasta antiga em Downloads\\dlb-sistema pode continuar onde está (é o código do programa).",
                        "Central DLB", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception e)
            {
                Log.Info("instalar " + p.Nome + ": " + e);
                Status("Não deu certo: " + e.Message);
                MessageBox.Show($"Não consegui instalar o {p.Nome}.\n\n{e.Message}", "Central DLB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally
            {
                ocupado = false;
                if (aba == "prog") Montar();
            }
        }

        // ================= BACKUP =================
        void MontarBackup()
        {
            // --- situação ---
            var k = new Cartao();
            DateTime.TryParse(cfg.UltimoBackup, out var ult);
            var nunca = string.IsNullOrEmpty(cfg.UltimoBackup);
            var velho = !nunca && (DateTime.Now - ult).TotalDays >= 3;
            k.Faixa = nunca || velho ? Tema.Laranja : Tema.Verde;
            k.Corpo.Controls.Add(Quebra(nunca ? "Nenhum backup feito ainda" : "Último backup: " + Sistema.Quando(ult), 14, nunca || velho ? Tema.Laranja : Tema.Verde, FontStyle.Bold));
            if (!string.IsNullOrEmpty(cfg.UltimoErro)) k.Corpo.Controls.Add(Quebra("⚠ " + cfg.UltimoErro, 9.5f, Tema.Vermelho));
            k.Corpo.Controls.Add(Quebra(cfg.Destinos.Count == 0
                ? "Escolha abaixo onde guardar: Google Drive (na nuvem) e/ou pendrive ou HD externo."
                : "O backup guarda os dados de todos os programas (e o código deles) em cada lugar escolhido. Dados: últimos 30 dias. Código: últimas 10 versões.", 10, Tema.Apagado));

            var auto = new CheckBox
            {
                Text = "Fazer sozinho: ao ligar o PC e de hora em hora (1 cópia por dia em cada lugar; o pendrive recebe quando for ligado)",
                Checked = cfg.Auto, AutoSize = true, ForeColor = Tema.Texto, BackColor = Tema.Cartao, Margin = new Padding(0, Tema.D(6), 0, Tema.D(4)),
            };
            auto.CheckedChanged += (s, e) =>
            {
                cfg.Auto = auto.Checked; cfg.Salvar();
                if (Instalacao.RodandoInstalada) Instalacao.BackupAutomatico(cfg.Auto);
                Status(cfg.Auto ? "Backup automático ligado." : "Backup automático desligado.");
            };
            k.Corpo.Controls.Add(auto);
            var l = Linha(); l.Margin = new Padding(0, Tema.D(8), 0, 0);
            var agora = Tema.Botao("💾  Fazer backup agora", true); agora.Enabled = cfg.Destinos.Count > 0;
            agora.Click += async (s, e) => await FazerBackup();
            var abrir = Tema.Botao("📂  Abrir pasta do backup");
            abrir.Enabled = cfg.Destinos.Any(d => Backup.Raiz(d) != null);
            abrir.Click += (s, e) => { var r = cfg.Destinos.Select(Backup.Raiz).FirstOrDefault(x => x != null && Directory.Exists(x)); if (r != null) Sistema.Abrir(r); else Status("A pasta ainda não existe: faça o primeiro backup."); };
            var rest = Tema.Botao("↩  Restaurar de um backup…"); rest.Click += (s, e) => Restaurar();
            l.Controls.AddRange(new Control[] { agora, abrir, rest });
            k.Corpo.Controls.Add(l);
            lista.Controls.Add(k);

            // --- onde guardar ---
            var w = new Cartao();
            w.Corpo.Controls.Add(Titulo("Onde guardar"));
            if (cfg.Destinos.Count == 0) w.Corpo.Controls.Add(Quebra("Nenhum lugar escolhido ainda.", 10, Tema.Apagado));
            foreach (var d in cfg.Destinos.ToList())
            {
                var raiz = Backup.Raiz(d);
                var linha = Linha(); linha.Margin = new Padding(0, 0, 0, Tema.D(6));
                var nome = Tema.Rotulo((d.Tipo == "drive" ? "☁  " : "💾  ") + d.Nome, 11, Tema.Texto, FontStyle.Bold);
                nome.Margin = new Padding(0, Tema.D(5), Tema.D(10), 0);
                linha.Controls.Add(nome);
                string est = raiz == null ? (d.Tipo == "drive" ? "Google Drive não encontrado neste PC" : "desligado agora — recebe o backup quando for ligado")
                                          : raiz;
                DateTime.TryParse(d.Ultimo, out var du);
                if (!string.IsNullOrEmpty(d.Ultimo)) est += "   ·   último: " + Sistema.Quando(du);
                var e2 = Tema.Rotulo(est, 9, raiz == null ? Tema.Laranja : Tema.Apagado); e2.Tag = "quebra"; e2.Margin = new Padding(0, Tema.D(8), Tema.D(10), 0);
                linha.Controls.Add(e2);
                var rm = Tema.Botao("Remover"); rm.Font = Tema.Fonte(8.5f); rm.Padding = new Padding(Tema.D(6), Tema.D(1), Tema.D(6), Tema.D(1));
                rm.Click += (s, e) =>
                {
                    if (MessageBox.Show($"Parar de guardar backup em \"{d.Nome}\"?\n(Os backups que já estão lá não são apagados.)", "Central DLB", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    cfg.Destinos.Remove(d); cfg.Salvar(); Montar();
                };
                linha.Controls.Add(rm);
                w.Corpo.Controls.Add(linha);
            }
            var l2 = Linha(); l2.Margin = new Padding(0, Tema.D(6), 0, 0);
            if (!cfg.Destinos.Any(d => d.Tipo == "drive"))
            {
                var gd = Tema.Botao("☁  + Google Drive", cfg.Destinos.Count == 0);
                gd.Click += (s, e) => AdicionarDrive();
                l2.Controls.Add(gd);
            }
            var pd = Tema.Botao("🔌  + Pendrive ou HD externo");
            pd.Click += (s, e) => AdicionarPasta();
            l2.Controls.Add(pd);
            w.Corpo.Controls.Add(l2);
            lista.Controls.Add(w);

            // --- o que entra ---
            var q = new Cartao();
            q.Corpo.Controls.Add(Titulo("O que entra no backup"));
            foreach (var f in Backup.Fontes())
            {
                var tem = f.Arquivo ? File.Exists(f.Origem) : Directory.Exists(f.Origem);
                q.Corpo.Controls.Add(Quebra((tem ? "✓  " : "—  ") + f.Descricao + (tem ? "" : " (não encontrado neste PC)"), 10, tem ? Tema.Texto : Tema.Apagado));
            }
            var sep = Tema.Rotulo("Código dos programas (só copia de novo quando algo muda):", 10, Tema.Apagado, FontStyle.Bold);
            sep.Margin = new Padding(0, Tema.D(12), 0, Tema.D(4));
            q.Corpo.Controls.Add(sep);
            if (cfg.Codigo.Count == 0) q.Corpo.Controls.Add(Quebra("Nenhuma pasta de código.", 10, Tema.Apagado));
            foreach (var pasta in cfg.Codigo.ToList())
            {
                var linha = Linha(); linha.Margin = new Padding(0, 0, 0, Tema.D(2));
                var ex = Directory.Exists(pasta);
                var t = Tema.Rotulo((ex ? "✓  " : "—  ") + pasta + (ex ? "" : " (não existe)"), 9.5f, ex ? Tema.Texto : Tema.Apagado);
                t.Tag = "quebra"; t.Margin = new Padding(0, Tema.D(5), Tema.D(8), 0);
                var rm = Tema.Botao("✕"); rm.Font = Tema.Fonte(8); rm.Padding = new Padding(Tema.D(4), 0, Tema.D(4), 0);
                rm.Click += (s, e) => { cfg.Codigo.Remove(pasta); cfg.Salvar(); Montar(); };
                linha.Controls.Add(t); linha.Controls.Add(rm);
                q.Corpo.Controls.Add(linha);
            }
            var add = Tema.Botao("+ Adicionar pasta");
            add.Margin = new Padding(0, Tema.D(8), 0, Tema.D(4));
            add.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "Escolha uma pasta para entrar no backup de código", SelectedPath = Locais.Downloads })
                    if (dlg.ShowDialog(this) == DialogResult.OK && !cfg.Codigo.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase))
                    { cfg.Codigo.Add(dlg.SelectedPath); cfg.Salvar(); Montar(); }
            };
            q.Corpo.Controls.Add(add);
            q.Corpo.Controls.Add(Quebra("Ficam de fora as pastas geradas sozinhas (node_modules, bin, obj, build, .git) e arquivos acima de 60 MB.", 8.5f, Tema.Apagado));
            lista.Controls.Add(q);
        }

        void AdicionarDrive()
        {
            var g = Backup.PastaGoogleDrive();
            if (g == null)
            {
                var msg = Backup.GoogleDriveInstalado
                    ? "O Google Drive está instalado, mas ainda não achei a pasta \"Meu Drive\".\n\nAbra o Google Drive (ícone perto do relógio), entre com a sua conta Google e clique de novo aqui."
                    : "Para guardar na nuvem, instale o \"Google Drive para computador\" (grátis, 15 GB) e entre com a sua conta Google.\n\nAbrir a página de download agora?";
                if (Backup.GoogleDriveInstalado) MessageBox.Show(msg, "Central DLB", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else if (MessageBox.Show(msg, "Central DLB", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Sistema.Abrir("https://www.google.com/intl/pt-BR/drive/download/");
                return;
            }
            cfg.Destinos.Add(new Destino { Tipo = "drive" });
            cfg.Salvar();
            Status("Google Drive adicionado: " + Path.Combine(g, "Backup DLB"));
            Montar();
        }

        void AdicionarPasta()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Escolha o pendrive ou HD externo (ou uma pasta dentro dele)" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var p = dlg.SelectedPath;
                var raizDisco = Path.GetPathRoot(p);
                try
                {
                    var di = new DriveInfo(raizDisco);
                    if (di.DriveType == DriveType.Fixed && string.Equals(raizDisco, Path.GetPathRoot(Locais.Usuario), StringComparison.OrdinalIgnoreCase)
                        && MessageBox.Show("Essa pasta fica no mesmo disco do Windows. Se o PC estragar, o backup vai junto.\n\nUsar assim mesmo?",
                               "Central DLB", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                }
                catch { }
                if (cfg.Destinos.Any(d => string.Equals(d.Caminho, p, StringComparison.OrdinalIgnoreCase))) return;
                cfg.Destinos.Add(Backup.NovoDestinoPasta(p));
                cfg.Salvar();
                Montar();
            }
        }

        async Task FazerBackup()
        {
            if (ocupado) return;
            ocupado = true;
            try
            {
                Status("Fazendo backup…", -1);
                var r = await Task.Run(() => Backup.Fazer(cfg, t => Status(t, -1)));
                if (r.Ok) Status("✓ Backup feito em: " + string.Join(", ", r.Feitos) + (r.Desligados.Count > 0 ? "   (desligado: " + string.Join(", ", r.Desligados) + ")" : ""));
                else if (r.Desligados.Count > 0) Status("Nenhum lugar de backup está ligado agora: " + string.Join(", ", r.Desligados));
                else Status(string.Join(" · ", r.Avisos));
            }
            catch (Exception e)
            {
                Log.Info("backup: " + e);
                cfg.UltimoErro = e.Message; cfg.Salvar();
                Status("Backup não foi feito: " + e.Message);
            }
            finally { ocupado = false; if (aba == "bk") Montar(); }
        }

        void Restaurar()
        {
            if (ocupado) return;
            var ini = cfg.Destinos.Select(Backup.Raiz).FirstOrDefault(x => x != null && Directory.Exists(Path.Combine(x, "dados")));
            using (var dlg = new OpenFileDialog
            {
                Title = "Escolha o backup de dados (dados-AAAA-MM-DD.zip)",
                Filter = "Backup de dados (dados-*.zip)|dados-*.zip|Todos os .zip|*.zip",
                InitialDirectory = ini != null ? Path.Combine(ini, "dados") : Locais.Usuario,
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var abertos = Backup.ProcessosQuePrecisamFechar.Where(Sistema.Aberto).ToList();
                if (abertos.Count > 0)
                {
                    MessageBox.Show("Feche estes programas antes de restaurar: " + string.Join(", ", abertos.Select(x => x == "DLBSistema" ? "DLB Sistema" : x)) +
                        "\n(o DLB Sistema fecha pelo ícone perto do relógio › Sair).", "Central DLB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (MessageBox.Show("Voltar os dados deste backup?\n\n" + Path.GetFileName(dlg.FileName) +
                        "\n\nNada é apagado: o que existe hoje fica guardado com o nome terminado em \".antes-\" + data.",
                        "Central DLB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    var n = Backup.Restaurar(dlg.FileName, t => Status(t, -1));
                    Status($"✓ {n} arquivos restaurados.");
                    MessageBox.Show($"Pronto: {n} arquivos voltaram para o lugar.\n\nAbra o DLB e o DLB Criativo e confira. Se algo não aparecer, " +
                        "use a opção de restaurar dentro do próprio programa com os arquivos da pasta \"backup-automatico\".", "Central DLB");
                }
                catch (Exception e)
                {
                    Log.Info("restaurar: " + e);
                    Status("Não deu para restaurar: " + e.Message);
                    MessageBox.Show("Não deu para restaurar:\n" + e.Message, "Central DLB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // ================= capturas para teste =================
        async Task SalvarCapturas()
        {
            Directory.CreateDirectory(capturaPasta);
            foreach (var t in new[] { "prog", "bk" })
            {
                Trocar(t);
                await Task.Delay(300);
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(Point.Empty, Size));
                    bmp.Save(Path.Combine(capturaPasta, t + ".png"));
                }
            }
        }
    }
}
