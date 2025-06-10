using System;
using System.IO;

namespace EDIFACTToSQL
{
    public class EDIProcessor
    {
        private readonly EDIFileHandler _fileHandler;
        private readonly EDIParser _parser;
        private readonly EDIDatabaseHandler _dbHandler;
        private readonly EmailService _emailService;

        public EDIProcessor(string directoryPath, string connectionString,
                           string smtpServer, int smtpPort, string smtpUsername,
                           string smtpPassword, string fromEmail, string toEmail)
        {
            _fileHandler = new EDIFileHandler(directoryPath ?? throw new ArgumentNullException(nameof(directoryPath)));
            _parser = new EDIParser();
            _dbHandler = new EDIDatabaseHandler(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
            _emailService = new EmailService(smtpServer, smtpPort, smtpUsername,
                                           smtpPassword, fromEmail, toEmail);
        }

        public void ProcessDirectory()
        {
            try
            {
                Console.WriteLine($"Processing EDI files from: {_fileHandler.EdiDirectoryPath}");

                var ediFiles = _fileHandler.GetEDIFiles();
                if (ediFiles.Length == 0)
                {
                    Console.WriteLine($"No .edi files found in directory: {_fileHandler.EdiDirectoryPath}");
                    return;
                }

                Console.WriteLine($"Found {ediFiles.Length} .edi files to process");

                foreach (var filePath in ediFiles)
                {
                    ProcessFileWithErrorHandling(filePath);
                }

                Console.WriteLine($"Finished processing {ediFiles.Length} files");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing directory: {ex.Message}");
            }
        }

        private void ProcessFileWithErrorHandling(string filePath)
        {
            try
            {
                Console.WriteLine($"Processing file: {Path.GetFileName(filePath)}");
                var ediContent = _fileHandler.ReadFileContent(filePath);
                var orderData = _parser.ParseEDIFACT(ediContent);

                if (!_dbHandler.IsDuplicateOrder(orderData))
                {
                    _dbHandler.InsertOrder(orderData, Path.GetFileName(filePath));
                    Console.WriteLine($"Successfully processed: {Path.GetFileName(filePath)}");

                    try
                    {
                        _emailService.SendOrderNotification(orderData, Path.GetFileName(filePath));
                    }
                    catch (Exception emailEx)
                    {
                        Console.WriteLine($"Warning: Order was processed but email notification failed: {emailEx.Message}");
                        
                    }
                }
                else
                {
                    Console.WriteLine($"Skipped duplicate order: {orderData.DocumentNumber}");
                }

                _fileHandler.ArchiveFile(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }
    }
}