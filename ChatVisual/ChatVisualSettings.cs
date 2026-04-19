

namespace ChatVisual
{
    internal class ChatVisualSettings
    {
        public string GlobalGuidelines { get; set; }
        public ApiKeySettings ApiKeys { get; set; }
        public ResponseModeSettingsSet Modes { get; set; }

        
        // the default static
        public static ChatVisualSettings CreateDefault()
        {
            var settings = new ChatVisualSettings();

            settings.GlobalGuidelines = "";
            settings.ApiKeys = new ApiKeySettings()
            {
                OpenAIApiKey = null,
                AnthropicApiKey = null,
            };

            settings.Modes = new ResponseModeSettingsSet()
            {
                Quick = new ResponseModeSettings()
                {
                    SystemPrompt = "You are a concise assistant. Answer correctly and briefly. If code is needed, write it cleanly.",
                    Primary = new AIProviderConfig()
                    {
                        Provider = AIProvider.OpenAI,
                        Model = "gpt-5.4-mini",
                        Temperature = 0.2f,
                        MaxOutputTokens = 1024
                    },

                    Fallback = null
                },


                Thinking = new ResponseModeSettings()
                {
                    SystemPrompt = "You are a careful coding assistant. Explain the approach briefly, then give the answer or code.",
                    Primary = new AIProviderConfig()
                    {
                        Provider = AIProvider.Anthropic,
                        Model = "claude-sonnet-4-6",
                        Temperature = 0.2f,
                        MaxOutputTokens = 2048
                    },
                    Fallback = null
                },

                DeepThinking = new ResponseModeSettings()
                {
                    SystemPrompt = "You are a deep reasoning coding assistant. Analyze the problem carefully, call out important tradeoffs, then give a clear solution.",
                    Primary = new AIProviderConfig()
                    {
                        Provider = AIProvider.Anthropic,
                        Model = "claude-sonnet-4-6",
                        Temperature = 0.2f,
                        MaxOutputTokens = 2048
                    },
                    Fallback = null
                }
            };

            return settings;
        }
    }
}
