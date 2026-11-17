namespace EncoreKit.Internal
{
    // How this package names itself to the native SDK, on every later analytics event and error report. make release
    // sets Version from package.json, and CI fails when the two differ.
    internal static class PackageInfo
    {
        internal const string Name = "unity";
        internal const string Version = "2.6.0";
    }
}
