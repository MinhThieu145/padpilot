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


        // AI agent parameters presets

        /// <summary>
        /// Parameters for fast, light weight response mode for OpenAI api
        /// </summary>
        private static readonly AIRequestConfig _lightweightResponseOpenAIConfig = new AIRequestConfig()
        {
            Model = "gpt-5.4-mini",
            Temperature = 0.2f,
            MaxOutputToken = 1024,
            SystemPrompt = "You are a concise assistant. Answer correctly and briefly. If the question requires code, write it cleanly. If it doesn't, just answer directly. No fluff.."
        };

        /// <summary>
        /// Parameters for a a good reasoning response mode for OpenAI api
        /// </summary>
        private static readonly AIRequestConfig _reasoningResponseOpenAIConfig = new AIRequestConfig()
        {
            Model = "gpt-5.4",
            Temperature = 0.2f,
            MaxOutputToken = 2048,
            SystemPrompt = "You are a Python coding assistant. You will receive screenshots of programming problems or code.\r\n\r\nIf you see a problem statement: briefly explain your approach in 2-4 sentences, then write the solution.\r\n\r\nIf you see someone's code: review it, point out what's wrong or what could be improved, then show the corrected version.\r\n\r\nCode style rules:\r\n- Write Python only\r\n- Prefer simple, readable solutions over clever ones\r\n- Add short, natural comments that explain the why, not the what\r\n- Avoid unnecessary abstractions or advanced features unless the problem genuinely needs them"

        };


        /// <summary>
        /// Parameters for fast, light weight response mode for Claude api
        /// </summary>
        private static readonly AIRequestConfig _lightweightResponseClaudeConfig = new AIRequestConfig()
        {
            Model = "claude-haiku-4-5",
            Temperature = 0.2f,
            MaxOutputToken = 1024,
            SystemPrompt = "You are a concise assistant. Answer correctly and briefly. If the question requires code, write it cleanly. If it doesn't, just answer directly. No fluff."
        };

        /// <summary>
        /// Parameters for a a good reasoning response mode for Claude api
        /// </summary>
        private static readonly AIRequestConfig _reasoningResponseClaudeConfig = new AIRequestConfig()
        {
            Model = "claude-sonnet-4-6",
            Temperature = 0.2f,
            MaxOutputToken = 2048,
            SystemPrompt = "You are a Python coding assistant. You will receive screenshots of programming problems or code.\r\n\r\nIf you see a problem statement: briefly explain your approach in 2-4 sentences, then write the solution.\r\n\r\nIf you see someone's code: review it, point out what's wrong or what could be improved, then show the corrected version.\r\n\r\nCode style rules:\r\n- Write Python only\r\n- Prefer simple, readable solutions over clever ones\r\n- Add short, natural comments that explain the why, not the what\r\n- Avoid unnecessary abstractions or advanced features unless the problem genuinely needs them"

        };






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
                    chatResponse = await _openAIWrapper.sendMessage(_sharedChatHistory, _lightweightResponseOpenAIConfig);
                    break;

                case ResponseMode.Thinking:
                    // we would do a more complex call, maybe with more context or a different model
                    try
                    {
                        chatResponse = await _claudeClient.sendMessage(_sharedChatHistory, _reasoningResponseClaudeConfig);
                    } catch
                    {
                        // we try again but now with the OpenAI as a backup plan
                        Console.WriteLine("[ModeOrchestrator] Claude call failed, falling back to OpenAI for Thinking mode.");
                        chatResponse = await _openAIWrapper.sendMessage(_sharedChatHistory, _reasoningResponseOpenAIConfig);
                    }
                    break;

                case ResponseMode.DeepThinking:
                    // we would do the most complex call, maybe with even more context or a more powerful model
                    try
                    {
                        chatResponse = await _claudeClient.sendMessage(_sharedChatHistory, _reasoningResponseClaudeConfig);
                    }
                    catch
                    {
                        // we try again but now with the OpenAI as a backup plan
                        Console.WriteLine("[ModeOrchestrator] Claude call failed, falling back to OpenAI for Thinking mode.");
                        chatResponse = await _openAIWrapper.sendMessage(_sharedChatHistory, _reasoningResponseOpenAIConfig);
                    }
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



        public void ClearHistory()
        {
            _sharedChatHistory.Clear();
        }

    }
}
