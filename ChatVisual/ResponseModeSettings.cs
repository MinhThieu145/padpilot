
namespace ChatVisual
{
    internal class ResponseModeSettings
    {
        public string SystemPrompt { get; set; }
        public AIProviderConfig Primary { get; set; }
        public AIProviderConfig Fallback { get; set; }
    }
}
