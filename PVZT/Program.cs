// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;

namespace PVZ
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            try
            {
                TaskbookUserFolders.EnsureCreated();
                Application.Run(new TeacherMenuForm());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(
                    $"Не удалось создать папки задачников в «Документы\\PVZ»:\n{exception.Message}",
                    "PVZT",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
