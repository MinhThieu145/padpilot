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

        // constructor
        public ModeOrchestrator()
        {
            // agent client
            _claudeClient = new ClaudeClient();
            _openAIWrapper = new OpenAIWrapper();

            // default mode
            _mode = ResponseMode.Quick;

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
                    chatResponse = await _openAIWrapper.sendMessage(messageText, messageScreenshots);
                    break;

                case ResponseMode.Thinking:
                    // we would do a more complex call, maybe with more context or a different model
                    chatResponse = await _claudeClient.sendMessage(messageText, messageScreenshots);
                    break;

                case ResponseMode.DeepThinking:
                    // we would do the most complex call, maybe with even more context or a more powerful model
                    chatResponse = await _claudeClient.sendMessage(messageText, messageScreenshots);
                    break;

                default:
                    // fail safe. This should not happen
                    throw new InvalidOperationException("Unknown response mode");

            }

            return chatResponse;
        }



    }
}
