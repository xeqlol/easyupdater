using System;
using System.IO;
using System.Net;
using System.Text;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Mail;
using System.Reflection;
using System.Threading;

namespace SOAUpdater
{
    class Program
    {
        // Timer variables.
        private static int updateCheckTimerInterval = 10 * 60 * 1000; // Every 10 minutes.

        // Server and archive variables.
        private static string updateURL = "<URL of the update server";
        private static string versionFile = "<file which contains NEW version of app>";
        private static string updateArchive = "<update archive>";

        // App location, app name, app start commands etc.
        private static string updateCheckFile = "<yourapp.exe>";
        private static string execFileCommands = "/yourappcommands";
        private static string appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static string updateDownloadDirectory = appDirectory + @"\update\";

        // Log file.
        private static string updateLogFile = "updatelog.txt";

        static void Main()
        {
            // Timer loop ;_; (yea, shitcode).
            do
            {
                TimerEventProcessor();
                Thread.Sleep(updateCheckTimerInterval);
            } while (true);
        }

        private static void TimerEventProcessor()
        {
            if (CheckUpdate())
            {
                Update();
            }
        }

        private static bool CheckUpdate()
        {
            WebClient client = new WebClient();

            // Check for update.
            string reply = client.DownloadString(updateURL + versionFile);
            long updateVersionNumber = long.Parse(reply.Replace(".", ""));
            long currentVersionNumber = long.Parse(AssemblyName.GetAssemblyName(updateCheckFile).Version.ToString().Replace(".", ""));

            // New and old version comparison.
            return updateVersionNumber > currentVersionNumber;
        }

        private static void Update()
        {
            // If app is running, then close it and run again after update.
            bool programRunning = false;

            // Update logger.
            StringBuilder logger = new StringBuilder();

            logger.AppendLine($"Update date and time: {DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")}");

            try
            {
                // Timer of update process.
                Stopwatch swatch = new Stopwatch();
                swatch.Start();

                logger.AppendLine($"Downloading update from: {updateURL + updateArchive}");

                WebClient downloader = new WebClient();

                // If update folder exists, then delete it.
                ClearAndDeleteDirectory(updateDownloadDirectory);
                Directory.CreateDirectory(updateDownloadDirectory);

                // Downloading update.
                downloader.DownloadFile(Path.Combine(updateURL, updateArchive),
                    Path.Combine(updateDownloadDirectory, updateArchive));
                downloader.Dispose();

                // Unzip update. You can rewrite it for your own needs.
                using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(updateDownloadDirectory, updateArchive)))
                {
                    logger.AppendLine($"Update archive unpacking: {updateArchive} ({new FileInfo(Path.Combine(updateDownloadDirectory, updateArchive)).Length} байт)\r\nФайлы архива:");
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        logger.AppendLine(entry.FullName);
                    }
                    archive.ExtractToDirectory(updateDownloadDirectory);
                }

                // Delete update archive.
                File.Delete(updateDownloadDirectory + updateArchive);

                // Kill running app.
                foreach (
                    var process in
                        Process.GetProcessesByName(
                            Path.GetFileNameWithoutExtension(Path.Combine(appDirectory, updateCheckFile))))
                {
                    programRunning = true;
                    process.Kill();
                }

                // Wait a little (need to kill all running app's process).
                Thread.Sleep(1000);

                // Copy and rewring new version of app and relete update folder.
                CopyDirectory(updateDownloadDirectory, appDirectory);
                ClearAndDeleteDirectory(updateDownloadDirectory);

                // Wait a little again.
                Thread.Sleep(1000);

                // Run updated app.
                if (programRunning)
                {
                    Process.Start(Path.Combine(appDirectory, updateCheckFile), execFileCommands);
                }
                
                // Stop time watching.
                swatch.Stop();

                logger.AppendLine($"Update successful! Update time elapsed: {swatch.ElapsedMilliseconds/1000} c.");

                // Send email about successful update.
                SendEmail($"Update successful!.\r\n\r\n{logger}");
            }
            catch (Exception ex)
            {
                // Try loop for some errors.
                try
                {
                    SendEmail($"OMG UPDATE ERROR!: {ex.Message}.\r\nText of exception:\r\n{ex}");
                }
                catch (Exception exep)
                {

                    logger.Append($"Email send error: {ex.Message}.\r\nText of exception:\r\n{exep}");
                }

                logger.Append($"OMG UPDATE ERROR!: {ex.Message}.\r\nText of exception:\r\n{ex}");
            }

            // Write in start of updatelog file.
            if (File.Exists(updateLogFile))
            {
                File.WriteAllText(updateLogFile, $"{logger}\r\n\r\n{File.ReadAllText(updateLogFile)}");
            }
            else
            {
                File.WriteAllText(updateLogFile, logger.ToString());
            }

            // Убираем мусор.
            GC.Collect();
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);

            foreach (var directory in Directory.GetDirectories(sourceDir))
                CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }

        private static void ClearAndDeleteDirectory(string sourceDir)
        {
            if (Directory.Exists(sourceDir))
            {
                string[] files = Directory.GetFiles(sourceDir);
                string[] dirs = Directory.GetDirectories(sourceDir);

                foreach (string file in files)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }

                foreach (string dir in dirs)
                {
                    ClearAndDeleteDirectory(dir);
                }

                Directory.Delete(sourceDir, false);
            }
        }

        private static void SendEmail(string message)
        {
            SmtpClient client = new SmtpClient();

            client.Port = 587;
            client.Host = "smtp.gmail.com";
            client.EnableSsl = true;
            client.Timeout = 10000;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential("<yourappemailadress>", "<yourappemailpassword>");

            string mainMessage = "Message";

            MailMessage mm = new MailMessage("<yourappemailadress>", "<yourmailadress>", "<title>", mainMessage);

            mm.BodyEncoding = UTF8Encoding.UTF8;
            mm.IsBodyHtml = true;
            mm.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;

            client.Send(mm);
            client.Dispose();
        }
    }
}
