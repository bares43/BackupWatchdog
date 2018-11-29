using System;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace BackupWatchdog {
    public static class Watchdog {

        private static IConfigurationRoot _config;
        private static ILogger _logger;

        [FunctionName("CloudBerryBackupWatchdog")]
        public static void Run([TimerTrigger("0 15 19 * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context) {

            _logger = log;
            _config = BuildConfig(context);

            try {
                var isBackupOk = CheckBackup();
                if (!isBackupOk) {
                    SendWarning();
                }
            } catch (Exception e) {
                _logger.LogError(e, e.Message);
            }

        }

        private static IConfigurationRoot BuildConfig(ExecutionContext context) {
            return new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        private static bool CheckBackup() {
            var isOk = false;
            using (var client = new ImapClient()) {

                client.Connect("imap.gmail.com", 993, true);

                client.Authenticate(_config["Email"], _config["Password"]);

                var backupFolder = client.GetFolder("Backup");
                backupFolder.Open(FolderAccess.ReadOnly);

                var query = SearchQuery.DeliveredAfter(DateTime.Now.Date).And(SearchQuery.SubjectContains("CloudBerry Backup completed"));

                isOk = backupFolder.Search(query).Count > 0;

                client.Disconnect(true);
            }

            return isOk;
        }

        private static void SendWarning() {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_config["Email"]));
            message.To.Add(new MailboxAddress(_config["Email"]));
            message.Subject = "Backup warning: no backup for today";

            message.Body = new TextPart("plain") {
                Text = @"Backup for today is missing."
            };

            using (var client = new SmtpClient()) {

                client.Connect("smtp.gmail.com", 587, false);

                client.Authenticate(_config["Email"], _config["Password"]);

                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}