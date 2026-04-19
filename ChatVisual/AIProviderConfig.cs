

namespace ChatVisual
{
    internal class AIProviderConfig
    {
        public AIProvider Provider { get; set; }
        public string Model { get; set; }
        public float Temperature { get; set; }
        public int MaxOutputTokens { get; set; }

    }
}
