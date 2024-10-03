using Vintagestory.API.MathTools;

namespace FancyClouds2D
{
    public class CloudProperties
    {
        public float CloudScale { get; set; }
        public float CloudAlpha { get; set; }
        public float CloudCover { get; set; }

        public CloudProperties(float scale, float alpha, float cover)
        {
            CloudScale = scale;
            CloudAlpha = alpha;
            CloudCover = cover;
        }

        // Lerp function to smoothly transition between two CloudProperties...
        public static CloudProperties Lerp(CloudProperties from, CloudProperties to, float t)
        {
            return new CloudProperties(GameMath.Lerp(from.CloudScale, to.CloudScale, t),
                GameMath.Lerp(from.CloudAlpha, to.CloudAlpha, t),
                GameMath.Lerp(from.CloudCover, to.CloudCover, t)
            );
        }
    }
}
