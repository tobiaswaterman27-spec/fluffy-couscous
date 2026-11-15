using System;
using System.IO;
using System.Text;

namespace AfterHours.Core
{
    /// <summary>
    /// Crash-safe file writing. Data is written to a temporary file first, flushed to disk,
    /// then swapped in. The previous version is kept as ".bak". If the game crashes mid-write,
    /// the old file is still intact. Used for settings now and for save games in Part 13.
    /// </summary>
    public static class SafeFile
    {
        public static void WriteAllTextAtomic(string path, string contents)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            string bak = path + ".bak";

            byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, bak);
                    return;
                }
                catch (Exception)
                {
                    // Some file systems do not support Replace; fall back to copy + move.
                }
                File.Copy(path, bak, true);
                File.Delete(path);
            }
            File.Move(tmp, path);
        }

        public static bool TryReadAllText(string path, out string text)
        {
            text = null;
            try
            {
                if (!File.Exists(path)) return false;
                text = File.ReadAllText(path, Encoding.UTF8);
                return !string.IsNullOrWhiteSpace(text);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
