// Claude client specific
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;



namespace ChatVisual
{
    internal class ClaudeClient
    {
        private static readonly string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");
        private List<Message> _chatHistory;

        // model parameter
        private MessageParameters _modelParams;

        // initialize the client
        AnthropicClient client;

        // the constructor for ClaudeClient
        public ClaudeClient()
        {
            // then we need to initialize the AnthropicClient
            client = new AnthropicClient(apiKey);
            _chatHistory = new List<Message>();

            // model paramter
            _modelParams = new MessageParameters()
            {
                Messages = _chatHistory,
                MaxTokens = 1024,
                Model = AnthropicModels.Claude46Sonnet,
                Stream = false
            };

        }

        // then we build the async sendMessage method
        // the method would return string
        public async Task<string> sendMessage(List<SharedMessage> sharedChatHistory, AIRequestConfig requestConfig)
        {
            // PERFORMANCE CONSIDERATION: at the moment we clear and populate entire chat histor for each request
            _chatHistory.Clear();

            foreach (SharedMessage sharedMessage in sharedChatHistory)
            {
                // adding the text and image to contentbase
                var contentParts = new List<ContentBase>();
                contentParts.Add(new TextContent() { Text = sharedMessage.Message });

                foreach (byte[] screenshot in sharedMessage.Screenshots)
                {
                    contentParts.Add(new ImageContent()
                    {
                        Source = new ImageSource()
                        {
                            MediaType = "image/png",
                            Data = Convert.ToBase64String(screenshot)
                        }
                    });
                }

                // now add the role and finish a message
                _chatHistory.Add(new Message()
                {
                    Role = sharedMessage.Role == ChatMessageRole.User ? RoleType.User : RoleType.Assistant,
                    Content = contentParts
                });


            }


            _modelParams = new MessageParameters()
            {
                Messages = _chatHistory,
                MaxTokens = requestConfig.MaxOutputToken,
                Temperature = (decimal)requestConfig.Temperature,
                Model = requestConfig.Model,
                System = new List<SystemMessage> { new SystemMessage(requestConfig.SystemPrompt) },
                Stream = false
            };



            // then we can simply call the api from the client
            int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var result = await client.Messages.GetClaudeMessageAsync(_modelParams);
                    return result.Message.ToString();

                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("Overloaded"))
                {
                    // we actually don't wait for the 3rd attemp
                    // cause after the 3rd attempt we stop anyways. So waiting at 3rd attempt is pointless
                    if (attempt < maxRetries - 1)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Claude failed after all retries", ex);
                }

            }
            throw new Exception("Claude failed after all retries");

        }


        // =====================================================================
        // UTILITIES
        // =====================================================================
        public void SessionCleaning()
        {
            _chatHistory.Clear();

        }
    }
}
