namespace FancyClouds2D
{
    public class ModConfig
    {
        public float CloudTypeTransitionSpeed { get; set; } = 0.03f;
        public float CloudMovementSpeed { get; set; } = 0.0025f;
        public float CloudPixelationAmount { get; set; } = 100f;
        public float RainCloudTransitionSpeed { get; set; } = 0.25f;
        public float CloudSimulationMultiplier { get; set; } = 2.5f;
        public string ModVersion { get; set; } = null;

        public ModConfig()
        {
            // Initialize default settings...
            CloudTypeTransitionSpeed = 0.03f;
            CloudMovementSpeed = 0.0025f;
            CloudPixelationAmount = 100f;
            RainCloudTransitionSpeed = 0.25f;
            CloudSimulationMultiplier = 2.5f;
            ModVersion = InitializeMod.ModInfo.Version;
        }

        public void FixMissingOrInvalidProperties(ModConfig defaultConfig)
        {
            System.Reflection.PropertyInfo[] properties = typeof(ModConfig).GetProperties();

            foreach(System.Reflection.PropertyInfo prop in properties)
            {
                object currentValue = prop.GetValue(this);
                object defaultValue = prop.GetValue(defaultConfig);

                // If current value is null or the same as default, replace it with the default value...
                if(currentValue == null || currentValue.Equals(defaultValue))
                {
                    prop.SetValue(this, defaultValue);
                }
            }
        }
    }
}
