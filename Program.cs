using FolderSync.Options;
using FolderSync.Logging;
using FolderSync.Sync;

namespace FolderSync
{
    internal static class Program
    {

        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            {
                PrintUsage();
                return 0;
            }

            try
            {
                var options = SyncOptions.Parse(args);

                var sourcePath  = Path.GetFullPath(options.Source)
                                      .TrimEnd(Path.DirectorySeparatorChar);
                var replicaPath = Path.GetFullPath(options.Replica)
                                      .TrimEnd(Path.DirectorySeparatorChar);

                ValidatePaths(sourcePath, replicaPath);

                using var logger = new Logger(options.LogFilePath); //logger
                logger.Info($"Starting:" + $"source='{sourcePath}', replica='{replicaPath}', " + $"interval={options.Interval} s, hashCompare={options.UseHash}.");

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    logger.Info("stopping...");
                    cts.Cancel();
                };

                var engine = new SyncEngine(logger);

    
                while (!cts.IsCancellationRequested) //main loop
                {
                    var cycleStart = DateTimeOffset.Now;
                    try
                    {
                        await engine.RunOnceAsync(sourcePath, replicaPath, options.UseHash, cts.Token);

                        var duration = (DateTimeOffset.Now - cycleStart).TotalSeconds;
                        logger.Info($"-- completed in {duration:F3}s. --");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex.Message);
                        logger.Info(ex.ToString());
                    }

                    try
                    {
                        await Task.Delay(options.Interval, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                logger.Info("stopped");
                return 0;
            }
            catch (OptionsException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void ValidatePaths(string source, string replica)
        {

            if (!Directory.Exists(source))
                throw new OptionsException($"folder does not exist: '{source}'");

            if (!Directory.Exists(replica))
                Directory.CreateDirectory(replica);

            if (string.Equals(source, replica,
                              StringComparison.OrdinalIgnoreCase))
            {
                throw new OptionsException(
                    "source and replica folder paths must be diffrent");
            }

            var sep = Path.DirectorySeparatorChar;
            if (source.StartsWith(replica + sep, StringComparison.OrdinalIgnoreCase)
             || replica.StartsWith(source + sep, StringComparison.OrdinalIgnoreCase))
            {
                throw new OptionsException(
                    "source and replica folders cannot be nested!");
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Help: ");
            Console.WriteLine(" FolderSync --source <folder path> --replica <replica folder path> --interval <seconds> --log <logfile path> [--hash]");
        }
    }
}
