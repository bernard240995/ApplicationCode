using System;
using System.IO;

namespace EDIFACTToSQL
{
    public class EDIFileHandler
    {
        private const string ArchiveSubdirectory = "Archive";
        public string EdiDirectoryPath { get; }

        public EDIFileHandler(string directoryPath)
        {
            EdiDirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            EnsureDirectoryExists(EdiDirectoryPath);
            EnsureDirectoryExists(Path.Combine(EdiDirectoryPath, ArchiveSubdirectory));
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"Created directory: {path}");
            }
        }

        public string[] GetEDIFiles()
        {
            try
            {
                return Directory.GetFiles(EdiDirectoryPath, "*.edi");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting EDI files: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public string ReadFileContent(string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
                throw;
            }
        }

        public void ArchiveFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                var archivePath = Path.Combine(EdiDirectoryPath, ArchiveSubdirectory);
                var destFile = Path.Combine(archivePath, Path.GetFileName(filePath));

                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }
                File.Move(filePath, destFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not archive file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
    }
}