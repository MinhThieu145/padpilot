

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
                    SystemPrompt = "You are a concise assistant. Answer correctly and briefly. If the question requires code, write it cleanly. If it doesn't, just answer directly. No fluff..",
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
                    SystemPrompt = "You are a Python coding assistant. You will receive screenshots of programming problems or code.\r\n\r\nIf you see a problem statement: briefly explain your approach in 2-4 sentences, then write the solution.\r\n\r\nIf you see someone's code: review it, point out what's wrong or what could be improved, then show the corrected version.\r\n\r\nCode style rules:\r\n- Write Python only\r\n- Prefer simple, readable solutions over clever ones\r\n- Add short, natural comments that explain the why, not the what\r\n- Avoid unnecessary abstractions or advanced features unless the problem genuinely needs them",
                    Primary = new AIProviderConfig()
                    {
                        Provider = AIProvider.Anthropic,
                        Model = "claude-sonnet-4-6",
                        Temperature = 0.2f,
                        MaxOutputTokens = 2048
                    },
                    Fallback = new AIProviderConfig()
                    {
                        Provider = AIProvider.OpenAI,
                        Model = "gpt-5.4",
                        Temperature = 0.2f,
                        MaxOutputTokens = 2048

                    }
                },

                DeepThinking = new ResponseModeSettings()
                {
                    SystemPrompt = "You are a Python coding assistant. You will receive screenshots of programming problems or code.\r\n\r\nIf you see a problem statement: briefly explain your approach in 2-4 sentences, then write the solution.\r\n\r\nIf you see someone's code: review it, point out what's wrong or what could be improved, then show the corrected version.\r\n\r\nCode style rules:\r\n- Write Python only\r\n- Prefer simple, readable solutions over clever ones\r\n- Add short, natural comments that explain the why, not the what\r\n- Avoid unnecessary abstractions or advanced features unless the problem genuinely needs them",
                    Primary = new AIProviderConfig()
                    {
                        Provider = AIProvider.Anthropic,
                        Model = "claude-sonnet-4-6",
                        Temperature = 0.2f,
                        MaxOutputTokens = 2048
                    },
                    Fallback = new AIProviderConfig()
                    {
                        Provider = AIProvider.OpenAI,
                        Model = "gpt-5.4",
                        Temperature = 0.2f,
                        MaxOutputTokens = 2048

                    }
                }
            };

            return settings;
        }
    }
}
