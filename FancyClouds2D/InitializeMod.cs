using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace FancyClouds2D
{
    public class InitializeMod : ModSystem
    {
        public static ICoreClientAPI ClientAPI { get; set; } = null;
        public static ModInfo ModInfo { get; set; } = null;

        readonly CloudRenderer2D cloudRenderer2D = new();

        public override void Start(ICoreAPI apiClient)
        {
            base.Start(apiClient);
            ModInfo = Mod.Info;
            Debug.LoadLogger(apiClient.Logger);
            Debug.Log($"Running on version: {Mod.Info.Version}");
            cloudRenderer2D.Patch();
        }

        public override void Dispose()
        {
            base.Dispose();
            cloudRenderer2D.Unpatch();
        }

        public override void StartClientSide(ICoreClientAPI apiClient)
        {
            base.StartClientSide(apiClient);
            ClientAPI = apiClient;

            CheckCreateConfig();

            cloudRenderer2D.Init(apiClient);
        }

        public void CheckCreateConfig()
        {
            ModConfig modConfiguration = null;
            ModConfig defaultConfig = new ModConfig(); // Create an instance of default config...

            try
            {
                modConfiguration = ClientAPI.LoadModConfig<ModConfig>("fancyclouds2d_config.json");
            }
            catch(Exception exception)
            {
                Debug.Log("Failed to load mod configuration...");
                modConfiguration = null;
            }

            if(modConfiguration == null)
            {
                Debug.Log("Generating new mod config (Config file is missing)!");
                modConfiguration = new ModConfig(); // Generate a new config with the default settings...
            }
            else
            {
                JsonObject configJson = ClientAPI.LoadModConfig("fancyclouds2d_config.json");

                // Check if the ModVersion is doesn't exist in the config, or mismatches with the current version...
                if(!configJson.KeyExists("ModVersion") || modConfiguration.ModVersion != ModInfo.Version)
                {
                    Debug.Log("Mod version mismatch or missing, regenerating config...");
                    modConfiguration = new ModConfig(); // Regenerate the config...
                }
                else
                {
                    // Fix missing or invalid properties in the loaded config...
                    modConfiguration.FixMissingOrInvalidProperties(defaultConfig);
                }
            }

            // Save the (potentially updated) config back to the file...
            ClientAPI.StoreModConfig(modConfiguration, "fancyclouds2d_config.json");

            ModConfig = modConfiguration;
        }

        // Load client-side only...
        public override bool ShouldLoad(EnumAppSide appSide) => appSide == EnumAppSide.Client;

        public static ModConfig ModConfig
        {
            get { return (ModConfig)ClientAPI.ObjectCache["fancyclouds2d_config.json"]; }
            set { ClientAPI.ObjectCache.Add("fancyclouds2d_config.json", value); }
        }
    }

    public class Debug
    {
        private static readonly OperatingSystem system = Environment.OSVersion;
        static private ILogger loggerUtility;

        static public void LoadLogger(ILogger logger) => loggerUtility = logger;

        static public void Log(string message)
        {
            if((system.Platform == PlatformID.Unix || system.Platform == PlatformID.Other) && Environment.UserInteractive)
            {
                Console.WriteLine($"{DateTime.Now:d.M.yyyy HH:mm:ss} [{InitializeMod.ModInfo.Name}] {message}");
            }
            else
            {
                loggerUtility?.Log(EnumLogType.Notification, $"[{InitializeMod.ModInfo.Name}] {message}");
            }
        }
    }
}
