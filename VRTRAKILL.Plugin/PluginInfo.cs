using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Plugin
{
    public static class PluginInfo
    {
        public const string
            PLUGIN_GUID = "VRTRAKILL", // do not change this string EVER
            PLUGIN_NAME = "VRTRAKILL",
            PLUGIN_VERSION = VRTRAKILL.BuildVersion.Value; // VRTRAKILLVersion in Directory.Build.props

        public static readonly string
            PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), // "~/BepInEx/plugins/VRTRAKILL"
            FullGamePath = Process.GetCurrentProcess().MainModule.FileName, // "~/ULTRAKILL/ULTRAKILL.exe"
            GamePath = Path.GetDirectoryName(FullGamePath); // "~/ULTRAKILL"
    }
}
