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


        // AI agent parameters presets

        /// <summary>
        /// Parameters for fast, light weight response mode for OpenAI api
        /// </summary>
        private static readonly AIRequestConfig _lightweightResponseOpenAIConfig = new AIRequestConfig()
        {
            Model = "gpt-5.4-mini",
            Temperature = 0.2f,
            MaxOutputToken = 1024,
            SystemPrompt = "You are a concise coding assistant. Give short, direct answers. No fluff."
        };

        /// <summary>
        /// Parameters for a a good reasoning response mode for OpenAI api
        /// </summary>
        private static readonly AIRequestConfig _reasoningResponseOpenAIConfig = new AIRequestConfig()
        {
            Model = "gpt-5.4",
            Temperature = 0.2f,
            MaxOutputToken = 2048,
            SystemPrompt = ""

        };


        /// <summary>
        /// Parameters for fast, light weight response mode for Claude api
        /// </summary>
        private static readonly AIRequestConfig _lightweightResponseClaudeConfig = new AIRequestConfig()
        {
            Model = "claude-haiku-4-5",
            Temperature = 0.2f,
            MaxOutputToken = 1024,
            SystemPrompt = ""
        };

        /// <summary>
        /// Parameters for a a good reasoning response mode for Claude api
        /// </summary>
        private static readonly AIRequestConfig _reasoningResponseClaudeConfig = new AIRequestConfig()
        {
            Model = "claude-sonnet-4-6",
            Temperature = 0.2f,
            MaxOutputToken = 2048,
            SystemPrompt = "You are an expert coding tutor helping a developer understand programming problems. \r\nExplain concepts clearly. When shown code or a problem, break down what's happening \r\nand guide toward the solution without giving it away directly."

        };






        // constructor
        public ModeOrchestrator()
        {
            // agent client
            _claudeClient = new ClaudeClient();
            _openAIWrapper = new OpenAIWrapper();

            // default mode
            _mode = ResponseMode.Quick;

            // event shout Mode Change.... we would clean the session
            OnModeChange += () =>
            {
                _openAIWrapper.SessionCleaning();
                _claudeClient.SessionCleaning();
            };

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
            switch ( _mode )
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
        public async Task<string> GetResponseAsync(string messageText, List<string> messageScreenshots)
        {
            string chatResponse = string.Empty;
            switch (_mode)
            {
                case ResponseMode.Quick:
                    // we would simply do a quick call
                    chatResponse = await _openAIWrapper.sendMessage(messageText, messageScreenshots, _lightweightResponseOpenAIConfig);
                    break;

                case ResponseMode.Thinking:
                    // we would do a more complex call, maybe with more context or a different model
                    chatResponse = await _claudeClient.sendMessage(messageText, messageScreenshots, _reasoningResponseClaudeConfig);
                    break;

                case ResponseMode.DeepThinking:
                    // we would do the most complex call, maybe with even more context or a more powerful model
                    chatResponse = await _claudeClient.sendMessage(messageText, messageScreenshots, _reasoningResponseClaudeConfig);
                    break;

                default:
                    // fail safe. This should not happen
                    throw new InvalidOperationException("Unknown response mode");

            }


            return chatResponse;
        }



    }
}
