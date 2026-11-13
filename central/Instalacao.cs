using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace CentralDlb
{
    /// <summary>A Central se instala sozinha: copia-se para AppData\Local\Programs, cria atalhos,
    /// aparece em "Aplicativos" do Windows e liga o backup automático.</summary>
    static class Instalacao
    {
        const string ChaveDesinstalar = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CentralDLB";
        const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Tarefa = "Central DLB - Backup";

        public static string ExeAtual => Process.GetCurrentProcess().MainModule.FileName;
        public static bool RodandoInstalada => string.Equals(Path.GetFullPath(ExeAtual), Path.GetFullPath(Locais.ExeInstalado), StringComparison.OrdinalIgnoreCase);

        /// <summary>Copia este .exe para a pasta de programas. Devolve true se deu certo.</summary>
        public static bool Instalar()
        {
            Directory.CreateDirectory(Locais.PastaInstalada);
            // a cópia antiga pode estar fechando (atualização): tenta por alguns segundos
            for (int i = 0; i < 40; i++)
            {
                try { File.Copy(ExeAtual, Locais.ExeInstalado, true); break; }
                catch (IOException) when (i < 39) { Thread.Sleep(250); }
            }
            var exe = Locais.ExeInstalado;
            Sistema.Atalho(Path.Combine(Sistema.AreaDeTrabalho, "Central DLB.lnk"), exe);
            Sistema.Atalho(Path.Combine(Sistema.MenuIniciar, "Central DLB.lnk"), exe);

            using (var k = Registry.CurrentUser.CreateSubKey(ChaveDesinstalar))
            {
                k.SetValue("DisplayName", "Central DLB");
                k.SetValue("DisplayVersion", Versao.Atual);
                k.SetValue("Publisher", "Danilo Bonani");
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", Locais.PastaInstalada);
                k.SetValue("UninstallString", $"\"{exe}\" --desinstalar");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            }
            Log.Info("Central instalada " + Versao.Atual);
            return true;
        }

        /// <summary>Backup automático: ao entrar no Windows e de hora em hora (só faz 1 vez por dia em cada destino).</summary>
        public static void BackupAutomatico(bool ligar)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(ChaveRun))
                {
                    if (ligar) k.SetValue("Central DLB Backup", $"\"{Locais.ExeInstalado}\" --auto");
                    else k.DeleteValue("Central DLB Backup", false);
                }
            }
            catch (Exception e) { Log.Info("Run: " + e.Message); }
            if (ligar)
                Sistema.Rodar("schtasks.exe", $"/Create /F /SC HOURLY /MO 1 /TN \"{Tarefa}\" /TR \"\\\"{Locais.ExeInstalado}\\\" --auto\"");
            else
                Sistema.Rodar("schtasks.exe", $"/Delete /F /TN \"{Tarefa}\"");
        }

        public static void Desinstalar()
        {
            BackupAutomatico(false);
            try { Registry.CurrentUser.DeleteSubKeyTree(ChaveDesinstalar, false); } catch { }
            foreach (var l in new[] { Path.Combine(Sistema.AreaDeTrabalho, "Central DLB.lnk"), Path.Combine(Sistema.MenuIniciar, "Central DLB.lnk") })
                try { File.Delete(l); } catch { }
            // apaga a pasta depois que este programa fechar (os dados e os backups ficam)
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{Locais.PastaInstalada}\"")
            { CreateNoWindow = true, UseShellExecute = false });
            Log.Info("Central desinstalada");
        }
    }
}
