using System;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace ChatVisual
{
    internal class ModeOrchestrator
    {

        private ResponseMode _mode;
        private ClaudeClient _claudeClient;
        private OpenAIWrapper _openAIWrapper;

        // our events to signify if the thinking mode has changed
        public event Action OnModeChange;

        /// <summary>
        /// The shared chat history that AI agents rely on. This shared history help we switch between agents without losing context
        /// </summary>
        public List<SharedMessage> _sharedChatHistory;


        /// <summary>
        /// Store the general settings
        /// </summary>
        private ChatVisualSettings _settings;

        // constructor
        public ModeOrchestrator()
        {
            // agent client
            _claudeClient = new ClaudeClient();
            _openAIWrapper = new OpenAIWrapper();

            // default mode
            _mode = ResponseMode.Quick;

            // the shared chat history
            _sharedChatHistory = new List<SharedMessage>();

            // event shout Mode Change.... we would clean the session
            OnModeChange += () =>
            {
                _sharedChatHistory.Clear();
            };


            _settings = ChatVisualSettings.CreateDefault();  // CreateDefault is a static method that return the default settings (no need the `new` keyword)

        }

        /// <summary>
        /// Handle the thinking mode
        /// </summary>
        public ResponseMode Mode
        {
            get { return _mode; }
            private set { _mode = value; }
        }


        /// <summary>
        /// Method to cycle through the mode. The mode will be cycled in predefined order: Quick -> Thinking -> DeepThinking -> Quick -> ..
        /// Cannot set a mode directly, only cycle through the mode.
        /// </summary>
        public void CycleThroughMode()
        {
            switch (_mode)
            {
                case ResponseMode.Quick:
                    _mode = ResponseMode.Thinking;
                    break;
                case ResponseMode.Thinking:
                    _mode = ResponseMode.DeepThinking;
                    break;
                case ResponseMode.DeepThinking:
                    _mode = ResponseMode.Quick;
                    break;
            }

            // after change the mode we have to scream "MODE CHANGE...." so our subscribers know
            OnModeChange?.Invoke();

            Console.WriteLine($"[ModeOrchestrator] Mode changed to {_mode}");
        }


        /// <summary>
        /// Handle the orchestration process to generate response from different agents
        /// </summary>
        public async Task<string> GetResponseAsync(string messageText, List<byte[]> messageScreenshots)
        {

            _sharedChatHistory.Add(new SharedMessage()
            {
                Role = ChatMessageRole.User,
                Message = messageText,
                Screenshots = messageScreenshots
            });

            string chatResponse = string.Empty;
            switch (_mode)
            {
                case ResponseMode.Quick:
                    // we would simply do a quick call
                    chatResponse = await SendWithModeSettingsAsync(_settings.Modes.Quick);
                    break;

                case ResponseMode.Thinking:
                    chatResponse = await SendWithModeSettingsAsync(_settings.Modes.Thinking);
                    break;

                case ResponseMode.DeepThinking:
                    chatResponse = await SendWithModeSettingsAsync(_settings.Modes.DeepThinking);
                    break;

                default:
                    // fail safe. This should not happen
                    throw new InvalidOperationException("Unknown response mode");
            }

            // done with the response, we add the assistant to our chat history
            _sharedChatHistory.Add(new SharedMessage()
            {
                Role = ChatMessageRole.Assistant,
                Message = chatResponse,
                Screenshots = new List<byte[]>()
            });

            // this response answer to the MainWindow, which know nothing of our chathistory other than the response we give back
            return chatResponse;
        }


        // =====================================================================
        // UTILITIES and HELPERS
        // =====================================================================

        public void ClearHistory()
        {
            _sharedChatHistory.Clear();
        }

        /// <summary>
        /// helper method to build the new AIRequestConfig based on the ResponseModeSettings (inside _settings) and AIProviderConfig (also inside _settings)
        /// </summary>
        private AIRequestConfig BuildRequestConfig(ResponseModeSettings responseModeSettings, AIProviderConfig aIProviderConfig)
        {
            AIRequestConfig newRequestConfig = new AIRequestConfig()
            {
                Model = aIProviderConfig.Model,
                SystemPrompt = responseModeSettings.SystemPrompt,
                Temperature = aIProviderConfig.Temperature,
                MaxOutputToken = aIProviderConfig.MaxOutputTokens,
            };

            return newRequestConfig;
        }

        /// <summary>
        /// helper method to send message to the AI client with the right provider
        /// </summary>
        /// <param name="aIProviderConfig"></param>
        /// <param name="aIRequestConfig"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<string> SendWithProviderAsync(AIProviderConfig aIProviderConfig, AIRequestConfig aIRequestConfig)
        {
            switch (aIProviderConfig.Provider)
            {
                case AIProvider.OpenAI:
                    return await _openAIWrapper.sendMessage(_sharedChatHistory, aIRequestConfig);
                case AIProvider.Anthropic:
                    return await _claudeClient.sendMessage(_sharedChatHistory, aIRequestConfig);
            }

            // we don't have this AI Provider (so if Gemini split in here somehow then we throw error)
            throw new InvalidOperationException("Unknown AI provider");
        }


        private async Task<string> SendWithModeSettingsAsync(ResponseModeSettings modeSettings)
        {
            try
            {
                AIRequestConfig requestConfig = BuildRequestConfig(modeSettings, modeSettings.Primary);
                return await SendWithProviderAsync(modeSettings.Primary, requestConfig);
            } catch
            {
                // if we have fallback we would fallback
                if (modeSettings.Fallback == null)
                {
                    throw;
                }
                else
                {
                    AIRequestConfig requestConfig = BuildRequestConfig(modeSettings, modeSettings.Fallback);
                    return await SendWithProviderAsync(modeSettings.Fallback, requestConfig);

                }
            }
        }


    }

}
