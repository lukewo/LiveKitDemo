namespace LiveKitDemo
{
    /// <summary>
    /// Default connection values for the demo.
    ///
    /// The API key and secret are NOT in source control. Put them in
    /// DemoConfig.Local.cs (git-ignored; copy DemoConfig.Local.cs.example).
    /// Without it the app still builds, and you paste an access token instead.
    ///
    /// DEMO ONLY: shipping an API secret inside an app lets anyone who unpacks the
    /// APK mint tokens for your project. Real apps fetch tokens from a backend.
    /// </summary>
    public static partial class DemoConfig
    {
        public const string ServerUrl = "wss://xamarindemo-zndglsm5.livekit.cloud";

        public const string DefaultRoom = "test-room";

        /// <summary>How long generated tokens stay valid.</summary>
        public static readonly System.TimeSpan TokenLifetime = System.TimeSpan.FromHours(6);

        /// <summary>LiveKit API key, set by DemoConfig.Local.cs. Empty if not configured.</summary>
        public static string ApiKey { get; private set; } = "";

        /// <summary>LiveKit API secret, set by DemoConfig.Local.cs. Empty if not configured.</summary>
        public static string ApiSecret { get; private set; } = "";

        static DemoConfig() => LoadLocalSettings();

        /// <summary>Implemented in DemoConfig.Local.cs when present; otherwise compiled away.</summary>
        static partial void LoadLocalSettings();
    }
}
