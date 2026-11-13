using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CentralDlb
{
    /// <summary>Cores e peças de tela (mesmo estilo do DLB Sistema e do DLB Criativo).</summary>
    static class Tema
    {
        public static readonly Color Fundo = Color.FromArgb(0x17, 0x17, 0x19);
        public static readonly Color Cartao = Color.FromArgb(0x22, 0x22, 0x26);
        public static readonly Color Borda = Color.FromArgb(0x33, 0x33, 0x38);
        public static readonly Color Texto = Color.FromArgb(0xEC, 0xEB, 0xE7);
        public static readonly Color Apagado = Color.FromArgb(0x9A, 0x9A, 0xA0);
        public static readonly Color Laranja = Color.FromArgb(0xF9, 0x7A, 0x1F);
        public static readonly Color Azul = Color.FromArgb(0x3B, 0x8B, 0xEB);
        public static readonly Color Creme = Color.FromArgb(0xEC, 0xEB, 0xE7);
        public static readonly Color Verde = Color.FromArgb(0x3E, 0xCF, 0x9A);
        public static readonly Color Vermelho = Color.FromArgb(0xFF, 0x6B, 0x6B);

        public static float Escala = 1f;
        public static int D(float px) => (int)Math.Round(px * Escala);

        static Icon icone;
        public static Icon Icone
        {
            get
            {
                if (icone == null)
                    using (var s = typeof(Tema).Assembly.GetManifestResourceStream("icone.ico")) icone = new Icon(s);
                return icone;
            }
        }

        public static Font Fonte(float pt, FontStyle st = FontStyle.Regular) => new Font("Segoe UI", pt, st);

        public static Label Rotulo(string t, float pt = 10, Color? cor = null, FontStyle st = FontStyle.Regular) => new Label
        {
            Text = t, AutoSize = true, Font = Fonte(pt, st), ForeColor = cor ?? Texto, BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, D(4)),
        };

        public static Button Botao(string t, bool principal = false, Color? cor = null)
        {
            var c = cor ?? (principal ? Laranja : Color.FromArgb(0x30, 0x30, 0x36));
            var b = new Button
            {
                Text = t, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat,
                BackColor = c, ForeColor = principal ? Color.FromArgb(0x17, 0x17, 0x19) : Texto,
                Font = Fonte(10, principal ? FontStyle.Bold : FontStyle.Regular), Cursor = Cursors.Hand,
                Padding = new Padding(D(12), D(5), D(12), D(5)), Margin = new Padding(0, 0, D(8), D(6)), UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(c, 0.15f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(c, 0.05f);
            return b;
        }

        public static GraphicsPath Arredondado(RectangleF r, float raio)
        {
            var p = new GraphicsPath(); var d = raio * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    /// <summary>Cartão de cantos arredondados que cresce conforme o conteúdo.</summary>
    class Cartao : Panel
    {
        public readonly FlowLayoutPanel Corpo;
        public Color? Faixa;  // risquinho colorido à esquerda

        public Cartao()
        {
            DoubleBuffered = true; BackColor = Tema.Fundo;
            Padding = new Padding(Tema.D(18), Tema.D(14), Tema.D(18), Tema.D(10));
            Margin = new Padding(0, 0, 0, Tema.D(12));
            Corpo = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown, WrapContents = false,
                BackColor = Tema.Cartao, Dock = DockStyle.Fill, Margin = Padding.Empty,
            };
            Controls.Add(Corpo);
        }

        /// <summary>Acerta a largura (e a altura pelo conteúdo). Rótulos com Tag "quebra" quebram a linha.</summary>
        public void Ajustar(int largura)
        {
            Width = largura;
            var dentro = largura - Padding.Horizontal;
            Quebrar(Corpo, dentro);
            Height = Corpo.GetPreferredSize(new Size(dentro, 0)).Height + Padding.Vertical;
            Invalidate();
        }

        static void Quebrar(Control c, int w)
        {
            foreach (Control f in c.Controls)
            {
                if (f is Label l && (l.Tag as string) == "quebra") l.MaximumSize = new Size(Math.Max(50, w - l.Margin.Horizontal), 0);
                else if (f is FlowLayoutPanel fl && fl.FlowDirection == FlowDirection.LeftToRight) fl.MaximumSize = new Size(w, 0);
                if (f.HasChildren) Quebrar(f, w);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var p = Tema.Arredondado(r, Tema.D(12)))
            using (var b = new SolidBrush(Tema.Cartao)) e.Graphics.FillPath(b, p);
            if (Faixa.HasValue)
                using (var b = new SolidBrush(Faixa.Value)) e.Graphics.FillRectangle(b, 0, Tema.D(14), Tema.D(4), Height - Tema.D(28));
        }
    }
}
