using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace FancyClouds2D
{
    [HarmonyPatchCategory("fancyclouds2d_cloudrenderer2d")]
    class CloudRenderer2D
    {
        public static ICoreClientAPI ClientAPI { get; set; } = null;
        public Harmony harmonyPatcher;
        private static float elapsedCloudTime;
        private static float currentPrecipitation;
        private static WeatherSystemClient WeatherSystem { get; set; } = null;

        private static CloudProperties currentCloudProperties;
        private static CloudProperties targetCloudProperties;
        private static float cloudTransitionTime = 0f;
        private static int lastWeatherIndex = -1; // To track changes in weather pattern

        public static float CloudTypeTransitionSpeed => InitializeMod.ModConfig.CloudTypeTransitionSpeed;
        public static float CloudMovementSpeed => InitializeMod.ModConfig.CloudMovementSpeed;
        public static float CloudPixelationAmount => InitializeMod.ModConfig.CloudPixelationAmount;
        public static float RainCloudTransitionSpeed => InitializeMod.ModConfig.RainCloudTransitionSpeed;
        public static float CloudSimulationMultiplier => InitializeMod.ModConfig.CloudSimulationMultiplier;

        public static Dictionary<int, CloudProperties> CloudTypes = new Dictionary<int, CloudProperties>
        {
            { 0, new CloudProperties(1.5f, 0.5f, 0.6f) }, // Altocumulus
            { 1, new CloudProperties(2.0f, 0.3f, 0.4f) }, // Cirrocumulus
            { 2, new CloudProperties(2.0f, 0.3f, 0.4f) }, // Cirrocumulus
            { 3, new CloudProperties(0.15f, 0.3f, 0.25f) }, // Clear Sky
            { 4, new CloudProperties(0.15f, 0.3f, 0.25f) }, // Clear Sky
            { 5, new CloudProperties(0.15f, 0.3f, 0.25f) }, // Clear Sky
            { 6, new CloudProperties(0.5f, 0.9f, 0.7f) }, // Cumulonimbus Clouds
            { 7, new CloudProperties(0.5f, 0.9f, 0.7f) }, // Cumulonimbus Clouds
            { 8, new CloudProperties(0.5f, 0.9f, 0.7f) }, // Cumulonimbus Clouds
            { 9, new CloudProperties(1.0f, 3.0f, 0.25f) }, // Cumulus Clouds
            { 10, new CloudProperties(1.0f, 3.0f, 0.25f) }, // Cumulus Clouds
            { 11, new CloudProperties(0.25f, 0.5f, 0.4f) }, // Haze
            { 12, new CloudProperties(0.25f, 0.5f, 0.4f) }, // Haze
            { 13, new CloudProperties(0.25f, 0.5f, 0.4f) }, // Haze
            { 14, new CloudProperties(0.8f, 1.0f, 1.0f) }, // Overcast
            { 15, new CloudProperties(0.8f, 1.0f, 1.0f) }, // Overcast
            { 16, new CloudProperties(0.8f, 1.0f, 1.0f) }, // Overcast
            { 17, new CloudProperties(1.5f, 0.3f, 0.2f) }, // Stratus Clouds
            { 18, new CloudProperties(1.5f, 0.3f, 0.2f) }, // Stratus Clouds
        };

        public void Init(ICoreClientAPI api)
        {
            ClientAPI = api;
            elapsedCloudTime = 0;
            lastWeatherIndex = -1;
            WeatherSystem = ClientAPI.ModLoader.GetModSystem<WeatherSystemClient>(true);

            ClientAPI.Event.LevelFinalize -= OnLevelFinalize;
            ClientAPI.Event.LevelFinalize += OnLevelFinalize;

            Debug.Log($"Initialized [{InitializeMod.ModInfo.Name}] {nameof(CloudRenderer2D)}!");
        }

        private void OnLevelFinalize()
        {
            WeatherSimulationRegion weatherSimulationRegion = GetWeatherSimulationRegion();
            if(weatherSimulationRegion == null) { return; }

            int currentWeatherIndex = GetWeatherPatternIndex(weatherSimulationRegion);

            if(currentWeatherIndex >= 0 && CloudTypes.ContainsKey(currentWeatherIndex))
            {
                currentCloudProperties = CloudTypes[currentWeatherIndex];
            }

            Debug.Log("Level finalization complete!");
        }

        public void Patch()
        {
            if(!Harmony.HasAnyPatches("fancyclouds2d_cloudrenderer2d"))
            {
                harmonyPatcher = new Harmony("fancyclouds2d_cloudrenderer2d");
                harmonyPatcher.PatchCategory("fancyclouds2d_cloudrenderer2d");
            }
        }

        public void Unpatch()
        {
            if(Harmony.HasAnyPatches("fancyclouds2d_cloudrenderer2d"))
            {
                harmonyPatcher.UnpatchAll();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CloudRenderer), "OnRenderFrame")]
        public static void OnRenderFrame(CloudRenderer __instance, float deltaTime, EnumRenderStage stage)
        {
            if(ClientAPI?.Side != EnumAppSide.Client || ClientAPI.InWorldEllapsedMilliseconds <= 0) { return; }

            ClimateCondition currentClimate = GetClimateCondition();
            WeatherSimulationRegion weatherSimulationRegion = GetWeatherSimulationRegion();

            if(weatherSimulationRegion == null || !weatherSimulationRegion.IsInitialized) { return; }

            // Stop current shader before updating...
            IShaderProgram currentShader = ClientAPI.Render.CurrentActiveShader;
            currentShader?.Stop();

            deltaTime *= CloudSimulationMultiplier;

            elapsedCloudTime += deltaTime;

            // Transition precipitation...
            currentPrecipitation = GameMath.Lerp(currentPrecipitation, currentClimate.Rainfall, RainCloudTransitionSpeed * deltaTime);
            currentPrecipitation = GameMath.Clamp(currentPrecipitation, 0f, 1f);

            UpdateCloudProperties(weatherSimulationRegion, deltaTime);

            // Update sky shader...
            if(currentCloudProperties != null)
            {
                UpdateSkyShader(new Dictionary<string, float>
                {
                    { "cloudCounter", elapsedCloudTime },
                    { "cloudPrecipitation", currentPrecipitation },
                    { "cloudScale", currentCloudProperties.CloudScale },
                    { "cloudAlpha", currentCloudProperties.CloudAlpha },
                    { "cloudCover", currentCloudProperties.CloudCover },
                    { "cloudSpeed", CloudMovementSpeed },
                    { "cloudPixelation", CloudPixelationAmount }
                });
            }

            // Start using the current shader again...
            currentShader?.Use();
        }

        private static void UpdateCloudProperties(WeatherSimulationRegion weatherSimulationRegion, float deltaTime)
        {
            if(weatherSimulationRegion?.NewWePattern == null) { return; }

            int currentWeatherIndex = GetWeatherPatternIndex(weatherSimulationRegion);

            //Debug.Log($"Current weather name: {weatherSimulationRegion.NewWePattern.GetWeatherName()}");
            //Debug.Log($"Current weather index: {currentWeatherIndex}");

            // Check if the weather pattern has changed...
            if(currentWeatherIndex != lastWeatherIndex && CloudTypes.ContainsKey(currentWeatherIndex))
            {
                targetCloudProperties = CloudTypes[currentWeatherIndex];
                lastWeatherIndex = currentWeatherIndex;
            }

            // Lerp towards the target cloud properties...
            if(targetCloudProperties != null && currentCloudProperties != null)
            {
                currentCloudProperties = CloudProperties.Lerp(currentCloudProperties, targetCloudProperties, CloudTypeTransitionSpeed * deltaTime);
            }
        }

        public static void UpdateSkyShader(Dictionary<string, float> uniformValues)
        {
            // Determine which shader program to use based on whether it's night time
            ShaderProgram shaderProgram = ShaderPrograms.Sky;

            // Ensure that the correct shader program is in use...
            shaderProgram.Use();

            // Iterate through the dictionary of uniform values and set each one...
            foreach(KeyValuePair<string, float> uniform in uniformValues)
            {
                shaderProgram.Uniform(uniform.Key, uniform.Value);
            }

            // Stop using the shader after setting the uniforms...
            shaderProgram.Stop();
        }

        public static WeatherSimulationRegion GetWeatherSimulationRegion()
        {
            if(ClientAPI?.World?.Player?.Entity == null) { return null; }
            int regionSize = ClientAPI.World.BlockAccessor.RegionSize;
            EntityPos playerPos = ClientAPI.World.Player.Entity.Pos;
            int playerRegionX = (int)playerPos.X / regionSize;
            int playerRegionZ = (int)playerPos.Z / regionSize;
            return WeatherSystem?.getOrCreateWeatherSimForRegion(playerRegionX, playerRegionZ);
        }

        public static ClimateCondition GetClimateCondition()
        {
            BlockPos playerPos = ClientAPI?.World?.Player?.Entity?.Pos?.AsBlockPos;
            IBlockAccessor blockAccessor = ClientAPI?.World?.BlockAccessor;
            return playerPos != null ? blockAccessor?.GetClimateAt(playerPos) : null;
        }

        private static int GetWeatherPatternIndex(WeatherSimulationRegion weatherSimulationRegion)
        {
            WeatherPattern currentWeatherPattern = weatherSimulationRegion.NewWePattern;
            string currentWeatherName = currentWeatherPattern?.GetWeatherName();
            List<WeatherPattern> weatherPatternsList = weatherSimulationRegion.WeatherPatterns.ToList();
            return weatherPatternsList.FindIndex(weatherPattern => weatherPattern.GetWeatherName() == currentWeatherName);
        }
    }
}
