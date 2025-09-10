namespace FolderSync.Options
{
    public sealed class SyncOptions
    {
        public string Source { get; }
        public string Replica { get; }
        public TimeSpan Interval { get; }
        public string LogFilePath { get; }
        public bool UseHash { get; }

        private SyncOptions(string source, string replica, TimeSpan interval, string logFilePath, bool useHash)
        {
            Source = Path.GetFullPath(source);
            Replica = Path.GetFullPath(replica);
            Interval = interval;
            LogFilePath = Path.GetFullPath(logFilePath);
            UseHash = useHash;
        }

        public static SyncOptions Parse(string[] args)
        {
            string? src = null, rep = null, log = null;
            int? seconds = null;
            bool useHash = false;

            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                switch (a)
                {
                    case "--source":
                        src = RequireNext(args, ref i, "--source");
                        break;
                    case "--replica":
                        rep = RequireNext(args, ref i, "--replica");
                        break;
                    case "--interval":
                        var val = RequireNext(args, ref i, "--interval");
                        if (!int.TryParse(val, out var s) || s <= 0)
                            throw new OptionsException("--interval must be a positive number");
                        seconds = s;
                        break;
                    case "--log":
                        log = RequireNext(args, ref i, "--log");
                        break;
                    case "--hash":
                        useHash = true;
                        break;
                    default:
                        throw new OptionsException($"unknown argument '{a}'");
                }
            }

            if (string.IsNullOrWhiteSpace(src)) throw new OptionsException("--source is required.");
            if (string.IsNullOrWhiteSpace(rep)) throw new OptionsException("--replica is required.");
            if (seconds is null) throw new OptionsException("--interval is required.");
            if (string.IsNullOrWhiteSpace(log)) throw new OptionsException("--log is required.");

            return new SyncOptions(src!, rep!, TimeSpan.FromSeconds(seconds!.Value), log!, useHash);
        }

        private static string RequireNext(string[] args, ref int i, string name)
        {
            if (i + 1 >= args.Length)
                throw new OptionsException($"missing value for {name}!");
            return args[++i];
        }
    }
}