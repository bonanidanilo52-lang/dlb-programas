using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CentralDlb
{
    /// <summary>Um lugar onde o backup é guardado (Google Drive ou pendrive/HD externo).</summary>
    public class Destino
    {
        public string Tipo { get; set; }     // "drive" ou "pasta"
        public string Caminho { get; set; }  // pasta escolhida (pendrive) — no Drive é descoberta sozinha
        public string Rotulo { get; set; }   // nome do pendrive, para achar mesmo se a letra mudar
        public string Relativo { get; set; } // caminho dentro do pendrive
        public string Ultimo { get; set; }   // data/hora do último backup feito aqui (ISO)

        public string Nome => Tipo == "drive" ? "Google Drive" : (string.IsNullOrEmpty(Rotulo) ? "Pasta" : Rotulo);
    }

    public class Config
    {
        public List<Destino> Destinos { get; set; } = new List<Destino>();
        public List<string> Codigo { get; set; } = new List<string>();
        public bool Auto { get; set; } = true;
        public int DiasDados { get; set; } = 30;
        public int CopiasCodigo { get; set; } = 10;
        public string UltimoBackup { get; set; }
        public string UltimoErro { get; set; }
        public bool CodigoPadraoFeito { get; set; }
        public bool CodigoLegendasFeito { get; set; }

        static string Arq => Path.Combine(Locais.Dados, "config.json");

        public static Config Carregar()
        {
            Config c = null;
            try { if (File.Exists(Arq)) c = Json.Ler<Config>(File.ReadAllText(Arq)); } catch (Exception e) { Log.Info("config: " + e.Message); }
            c = c ?? new Config();
            c.Destinos = c.Destinos ?? new List<Destino>();
            c.Codigo = c.Codigo ?? new List<string>();
            if (!c.CodigoPadraoFeito)
            {
                // pastas de código do Danilo (só as que existem neste PC)
                foreach (var n in new[] { @"studio-fonte\studio-fonte", "studio-computador", "dlb-criativo", "dlb-sistema", "dlb-programas", "Portif-lioDanilo" })
                {
                    var p = Path.Combine(Locais.Downloads, n);
                    if (Directory.Exists(p) && !c.Codigo.Contains(p, StringComparer.OrdinalIgnoreCase)) c.Codigo.Add(p);
                }
                c.CodigoPadraoFeito = true;
            }
            if (!c.CodigoLegendasFeito)
            {
                // DLB Legendas (plugin do Premiere) entrou depois: acrescenta o código dele uma vez
                foreach (var n in new[] { "dlb-legendas", "dlb-whisper" })
                {
                    var p = Path.Combine(Locais.Downloads, n);
                    if (Directory.Exists(p) && !c.Codigo.Contains(p, StringComparer.OrdinalIgnoreCase)) c.Codigo.Add(p);
                }
                c.CodigoLegendasFeito = true;
            }
            return c;
        }

        public void Salvar()
        {
            try
            {
                Directory.CreateDirectory(Locais.Dados);
                File.WriteAllText(Arq + ".tmp", Json.Escrever(this));
                if (File.Exists(Arq)) File.Delete(Arq);
                File.Move(Arq + ".tmp", Arq);
            }
            catch (Exception e) { Log.Info("salvar config: " + e.Message); }
        }
    }

    /// <summary>Uma pasta (ou arquivo) de dados que entra no backup e para onde ela volta ao restaurar.</summary>
    class FonteDados
    {
        public string Prefixo, Origem, Descricao;
        public bool Arquivo;
        public string[] SoSubpastas; // se preenchido, pega só estas subpastas (ex.: Local Storage do Electron)
    }

    static class Backup
    {
        public static List<FonteDados> Fontes()
        {
            var doc = Locais.Documentos; var ap = Locais.Roaming;
            return new List<FonteDados>
            {
                new FonteDados { Prefixo = "DLB/backup-automatico", Origem = Path.Combine(doc, "Studio-Backup"), Descricao = "DLB — backups automáticos (clientes, trabalhos, financeiro)" },
                new FonteDados { Prefixo = "DLB/dados-do-programa", Origem = Path.Combine(ap, "studio"), SoSubpastas = new[] { "Local Storage", "IndexedDB" }, Descricao = "DLB — dados guardados dentro do programa" },
                new FonteDados { Prefixo = "DLB Criativo/backup-automatico", Origem = Path.Combine(doc, "DLB-Criativo-Backup"), Descricao = "DLB Criativo — backups automáticos (quadros e imagens)" },
                new FonteDados { Prefixo = "DLB Criativo/dados-do-programa", Origem = Path.Combine(ap, "dlb-criativo"), SoSubpastas = new[] { "Local Storage", "IndexedDB" }, Descricao = "DLB Criativo — dados guardados dentro do programa" },
                new FonteDados { Prefixo = "DLB Sistema", Origem = Path.Combine(ap, "DLB Sistema"), Descricao = "DLB Sistema — ajustes, uso do PC e partidas" },
                new FonteDados { Prefixo = "DLB Legendas/dados.json", Origem = Path.Combine(doc, "DLB Legendas", "dados.json"), Arquivo = true, Descricao = "DLB Legendas — seus modelos, correções aprendidas e ajustes" },
                new FonteDados { Prefixo = "Plano Instagram/plano-instagram-dlb.json", Origem = Path.Combine(doc, "plano-instagram-dlb.json"), Arquivo = true, Descricao = "Plano do Instagram" },
            };
        }

        // pastas que nunca entram no backup de código (são geradas sozinhas e pesam muito)
        static readonly HashSet<string> Ignorar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "node_modules", "bin", "obj", "dist", "out", ".gradle", "build", ".git", ".vs", ".idea", "DLB Sistema", "Cache", "Code Cache", "GPUCache" };
        const long MaxArquivoCodigo = 60L * 1024 * 1024;

        /// <summary>Acha a pasta "Meu Drive" do Google Drive para computador.</summary>
        public static string PastaGoogleDrive()
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    foreach (var n in new[] { "Meu Drive", "My Drive" })
                    {
                        var p = Path.Combine(d.RootDirectory.FullName, n);
                        if (Directory.Exists(p)) return p;
                    }
                }
                catch { }
            }
            // modo antigo (pasta espelhada)
            foreach (var n in new[] { "Meu Drive", "My Drive", "Google Drive" })
            {
                var p = Path.Combine(Locais.Usuario, n);
                if (Directory.Exists(p)) return p;
            }
            return null;
        }

        public static bool GoogleDriveInstalado =>
            Directory.Exists(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "Google", "Drive File Stream"));

        /// <summary>Pasta "Backup DLB" do destino, ou null se ele não está ligado agora.</summary>
        public static string Raiz(Destino d)
        {
            if (d.Tipo == "drive")
            {
                var g = PastaGoogleDrive();
                return g == null ? null : Path.Combine(g, "Backup DLB");
            }
            if (!string.IsNullOrEmpty(d.Caminho) && Directory.Exists(d.Caminho)) return Path.Combine(d.Caminho, "Backup DLB");
            // a letra do pendrive mudou? procura pelo nome
            if (!string.IsNullOrEmpty(d.Rotulo))
                foreach (var dr in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (dr.IsReady && dr.VolumeLabel == d.Rotulo)
                        {
                            var p = Path.Combine(dr.RootDirectory.FullName, d.Relativo ?? "");
                            if (Directory.Exists(p)) return Path.Combine(p, "Backup DLB");
                        }
                    }
                    catch { }
                }
            return null;
        }

        public static Destino NovoDestinoPasta(string pasta)
        {
            var d = new Destino { Tipo = "pasta", Caminho = pasta };
            try
            {
                var raiz = Path.GetPathRoot(pasta);
                var di = new DriveInfo(raiz);
                d.Rotulo = string.IsNullOrWhiteSpace(di.VolumeLabel) ? raiz.TrimEnd('\\') : di.VolumeLabel;
                d.Relativo = pasta.Substring(raiz.Length);
            }
            catch { d.Rotulo = pasta; }
            return d;
        }

        static IEnumerable<FileInfo> ArquivosCodigo(DirectoryInfo dir)
        {
            FileInfo[] fs = new FileInfo[0];
            DirectoryInfo[] ds = new DirectoryInfo[0];
            try { fs = dir.GetFiles(); ds = dir.GetDirectories(); } catch { }
            foreach (var f in fs)
                if (f.Length <= MaxArquivoCodigo && (f.Attributes & FileAttributes.ReparsePoint) == 0) yield return f;
            foreach (var d in ds)
            {
                if (Ignorar.Contains(d.Name) || (d.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                foreach (var f in ArquivosCodigo(d)) yield return f;
            }
        }

        static IEnumerable<FileInfo> ArquivosDados(FonteDados f)
        {
            if (f.Arquivo) { var fi = new FileInfo(f.Origem); if (fi.Exists) yield return fi; yield break; }
            var raiz = new DirectoryInfo(f.Origem);
            if (!raiz.Exists) yield break;
            var pastas = f.SoSubpastas == null ? new[] { raiz } : f.SoSubpastas.Select(s => new DirectoryInfo(Path.Combine(f.Origem, s))).Where(d => d.Exists).ToArray();
            foreach (var p in pastas)
            {
                FileInfo[] todos;
                try { todos = p.GetFiles("*", SearchOption.AllDirectories); } catch { continue; }
                foreach (var a in todos) yield return a;
            }
        }

        static string Relativo(string baseDir, string arq) => arq.Substring(baseDir.TrimEnd('\\').Length + 1).Replace('\\', '/');

        /// <summary>Copia um arquivo para dentro do zip mesmo se o programa estiver com ele aberto.</summary>
        static bool Adicionar(ZipArchive z, string arq, string nome)
        {
            try
            {
                using (var ent = new FileStream(arq, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var e = z.CreateEntry(nome, CompressionLevel.Optimal);
                    e.LastWriteTime = File.GetLastWriteTime(arq);
                    using (var s = e.Open()) ent.CopyTo(s);
                }
                return true;
            }
            catch (Exception ex) { Log.Info("pulou " + arq + ": " + ex.Message); return false; }
        }

        static string CriarZipDados(string destino, Action<string> av)
        {
            int n = 0, pulados = 0;
            var leia = new StringBuilder();
            leia.AppendLine("BACKUP DOS DADOS — Central DLB");
            leia.AppendLine("Feito em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " no PC " + Environment.MachineName);
            leia.AppendLine();
            leia.AppendLine("Para voltar tudo: abra a Central DLB › Backup › Restaurar de um backup, e escolha este arquivo.");
            leia.AppendLine("Ou, à mão: o DLB e o DLB Criativo importam os arquivos .json das pastas \"backup-automatico\"");
            leia.AppendLine("(DLB: Ajustes › Backup › Restaurar; DLB Criativo: ☰ › Abrir backup).");
            leia.AppendLine();
            var mapa = new StringBuilder();
            using (var fs = new FileStream(destino, FileMode.Create))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var f in Fontes())
                {
                    av("Juntando: " + f.Descricao);
                    int antes = n;
                    foreach (var a in ArquivosDados(f))
                    {
                        var nome = f.Arquivo ? f.Prefixo : f.Prefixo + "/" + Relativo(f.Origem, a.FullName);
                        if (Adicionar(z, a.FullName, nome)) n++; else pulados++;
                    }
                    leia.AppendLine((n > antes ? "✓ " : "— (não encontrado) ") + f.Descricao);
                    if (n > antes) mapa.AppendLine(f.Prefixo + "|" + f.Origem);
                }
                var cfg = Path.Combine(Locais.Dados, "config.json");
                if (File.Exists(cfg)) Adicionar(z, cfg, "Central DLB/config.json");
                using (var w = new StreamWriter(z.CreateEntry("LEIA-ME.txt").Open(), new UTF8Encoding(true))) w.Write(leia.ToString());
                using (var w = new StreamWriter(z.CreateEntry("_mapa.txt").Open(), new UTF8Encoding(false))) w.Write(mapa.ToString());
            }
            if (n == 0) throw new Exception("Nenhum dado encontrado para guardar (os programas ainda não foram usados neste PC?).");
            Log.Info($"dados.zip: {n} arquivos, {pulados} pulados, {Sistema.Tamanho(new FileInfo(destino).Length)}");
            return pulados > 0 ? $"{pulados} arquivo(s) em uso foram pulados" : null;
        }

        static void CriarZipCodigo(Config c, string destino, Action<string> av)
        {
            int n = 0;
            using (var fs = new FileStream(destino, FileMode.Create))
            using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var pasta in c.Codigo.Where(Directory.Exists))
                {
                    var nomeBase = new DirectoryInfo(pasta).Name;
                    av("Juntando o código: " + nomeBase);
                    foreach (var a in ArquivosCodigo(new DirectoryInfo(pasta)))
                        if (Adicionar(z, a.FullName, nomeBase + "/" + Relativo(pasta, a.FullName))) n++;
                }
            }
            Log.Info($"codigo.zip: {n} arquivos, {Sistema.Tamanho(new FileInfo(destino).Length)}");
        }

        /// <summary>Data do arquivo de código mais novo (para só copiar o código quando ele mudou).</summary>
        static DateTime CodigoMaisNovo(Config c)
        {
            var max = DateTime.MinValue;
            foreach (var pasta in c.Codigo.Where(Directory.Exists))
                foreach (var a in ArquivosCodigo(new DirectoryInfo(pasta)))
                    if (a.LastWriteTime > max) max = a.LastWriteTime;
            return max;
        }

        static FileInfo[] Copias(string pasta, string padrao)
        {
            var d = new DirectoryInfo(pasta);
            return d.Exists ? d.GetFiles(padrao).OrderByDescending(f => f.Name).ToArray() : new FileInfo[0];
        }

        public static bool FeitoHoje(Destino d)
        {
            var r = Raiz(d);
            return r != null && File.Exists(Path.Combine(r, "dados", $"dados-{DateTime.Today:yyyy-MM-dd}.zip"));
        }

        public class Resultado { public List<string> Feitos = new List<string>(), Desligados = new List<string>(), Avisos = new List<string>(); public bool Ok => Feitos.Count > 0; }

        /// <summary>Faz o backup em todos os destinos que estiverem ligados.</summary>
        public static Resultado Fazer(Config c, Action<string> av, bool soSeFaltar = false)
        {
            var res = new Resultado();
            var ligados = new List<(Destino d, string raiz)>();
            foreach (var d in c.Destinos)
            {
                var r = Raiz(d);
                if (r == null) { res.Desligados.Add(d.Nome); continue; }
                if (soSeFaltar && FeitoHoje(d)) continue;
                ligados.Add((d, r));
            }
            if (c.Destinos.Count == 0) { res.Avisos.Add("Nenhum lugar de backup escolhido."); return res; }
            if (ligados.Count == 0) return res;

            Directory.CreateDirectory(Locais.Temp);
            var stamp = DateTime.Now;
            var zDados = Path.Combine(Locais.Temp, "dados.zip");
            var aviso = CriarZipDados(zDados, av);
            if (aviso != null) res.Avisos.Add(aviso);

            // código: só quando mudou desde a última cópia daquele destino
            DateTime? novo = null;
            string zCod = null;
            foreach (var (d, raiz) in ligados)
            {
                try
                {
                    av("Copiando para " + d.Nome + "…");
                    var pd = Path.Combine(raiz, "dados");
                    Directory.CreateDirectory(pd);
                    var alvo = Path.Combine(pd, $"dados-{stamp:yyyy-MM-dd}.zip");
                    File.Copy(zDados, alvo + ".tmp", true);
                    if (File.Exists(alvo)) File.Delete(alvo);
                    File.Move(alvo + ".tmp", alvo);
                    // guarda os últimos N dias (no mínimo 3 cópias)
                    var ds = Copias(pd, "dados-*.zip");
                    foreach (var f in ds.Skip(3).Where(f => f.LastWriteTime < DateTime.Now.AddDays(-c.DiasDados))) f.Delete();

                    if (c.Codigo.Any(Directory.Exists))
                    {
                        var pc = Path.Combine(raiz, "codigo");
                        Directory.CreateDirectory(pc);
                        var ult = Copias(pc, "codigo-*.zip").FirstOrDefault();
                        if (novo == null) { av("Conferindo se o código mudou…"); novo = CodigoMaisNovo(c); }
                        if (ult == null || novo.Value > ult.LastWriteTime)
                        {
                            if (zCod == null) { zCod = Path.Combine(Locais.Temp, "codigo.zip"); CriarZipCodigo(c, zCod, av); }
                            av("Copiando o código para " + d.Nome + "…");
                            var ac = Path.Combine(pc, $"codigo-{stamp:yyyy-MM-dd-HHmm}.zip");
                            File.Copy(zCod, ac + ".tmp", true);
                            File.Move(ac + ".tmp", ac);
                            foreach (var f in Copias(pc, "codigo-*.zip").Skip(Math.Max(1, c.CopiasCodigo))) f.Delete();
                        }
                    }
                    d.Ultimo = stamp.ToString("o");
                    res.Feitos.Add(d.Nome);
                }
                catch (Exception e)
                {
                    Log.Info("backup em " + d.Nome + ": " + e);
                    res.Avisos.Add(d.Nome + ": " + e.Message);
                }
            }
            try { File.Delete(zDados); if (zCod != null) File.Delete(zCod); } catch { }
            if (res.Ok) c.UltimoBackup = stamp.ToString("o");
            c.UltimoErro = res.Avisos.Count > 0 ? string.Join(" · ", res.Avisos) : null;
            c.Salvar();
            Log.Info("backup: feito em " + string.Join(", ", res.Feitos) + (res.Desligados.Count > 0 ? "; desligados: " + string.Join(", ", res.Desligados) : ""));
            return res;
        }

        public static readonly string[] ProcessosQuePrecisamFechar = { "DLB", "DLB Criativo", "DLBSistema" };

        /// <summary>Volta os dados de um dados-*.zip para os lugares de origem. O que existia antes é guardado com ".antes-…".</summary>
        public static int Restaurar(string zip, Action<string> av)
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            int n = 0;
            using (var z = ZipFile.OpenRead(zip))
            {
                var me = z.GetEntry("_mapa.txt") ?? throw new Exception("Este arquivo não é um backup de dados da Central DLB.");
                string mapa;
                using (var r = new StreamReader(me.Open())) mapa = r.ReadToEnd();
                foreach (var linha in mapa.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = linha.Split('|');
                    if (p.Length != 2) continue;
                    var prefixo = p[0]; var origem = p[1];
                    av("Restaurando " + prefixo + "…");
                    var entradas = z.Entries.Where(e => e.FullName == prefixo || e.FullName.StartsWith(prefixo + "/")).ToList();
                    if (entradas.Count == 0) continue;
                    var ehArquivo = entradas.Count == 1 && entradas[0].FullName == prefixo;
                    // guarda o que existe hoje
                    if (ehArquivo && File.Exists(origem)) File.Move(origem, origem + ".antes-" + stamp);
                    if (!ehArquivo && Directory.Exists(origem))
                    {
                        if (prefixo.EndsWith("dados-do-programa"))
                        {
                            foreach (var sub in new[] { "Local Storage", "IndexedDB" })
                            {
                                var sp = Path.Combine(origem, sub);
                                if (Directory.Exists(sp)) Directory.Move(sp, sp + ".antes-" + stamp);
                            }
                        }
                        else Directory.Move(origem, origem + ".antes-" + stamp);
                    }
                    foreach (var e in entradas)
                    {
                        if (string.IsNullOrEmpty(e.Name)) continue;
                        var alvo = ehArquivo ? origem : Path.Combine(origem, e.FullName.Substring(prefixo.Length + 1).Replace('/', '\\'));
                        Directory.CreateDirectory(Path.GetDirectoryName(alvo));
                        e.ExtractToFile(alvo, true);
                        n++;
                    }
                }
            }
            Log.Info($"restaurado {zip}: {n} arquivos");
            return n;
        }
    }
}
