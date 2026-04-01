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
        private List<Message> _messageHistory;

        // model parameter
        private MessageParameters _modelParams;

        // initialize the client
        AnthropicClient client;

        // the constructor for ClaudeClient
        public ClaudeClient()
        {
            // then we need to initialize the AnthropicClient
            client = new AnthropicClient(apiKey);
            _messageHistory = new List<Message>();

            // model paramter
            _modelParams = new MessageParameters()
            {
                Messages = _messageHistory,
                MaxTokens = 1024,
                Model = AnthropicModels.Claude46Sonnet,
                Stream = false
            };

        }

        // then we build the async sendMessage method
        // the method would return string
        public async Task<string> sendMessage(string message, List<String> screenShots, AIRequestConfig requestConfig)
        {
            _modelParams = new MessageParameters()
            {
                Messages = _messageHistory,
                MaxTokens = requestConfig.MaxOutputToken,
                Temperature = (decimal)requestConfig.Temperature,
                Model = requestConfig.Model,
                System = new List<SystemMessage> { new SystemMessage(requestConfig.SystemPrompt)},
                Stream = false
            };

            // we add our current message to our _messageHistory list
            if (screenShots.Count == 0)
            {
                Console.WriteLine("Message sent without screenshot");
                _messageHistory.Add(new Message(RoleType.User, message));

            }
            else
            {
                Console.WriteLine("Message sent with screenshot");

                List<ContentBase> contentBases = new List<ContentBase>();

                // add all the screenshots to Content
                foreach (string screenShot in screenShots)
                {
                    contentBases.Add(

                        new ImageContent()
                        {
                            Source = new ImageSource()
                            {
                                MediaType = "image/png",
                                Data = screenShot
                            }
                        }

                    );
                }

                // add the text message to content
                contentBases.Add(

                    new TextContent()
                    {
                        Text = message,
                    }

                );

                _messageHistory.Add(new Message()
                {
                    Role = RoleType.User,
                    Content = contentBases
                });
            }

            // then we can simply call the api from the client
            int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {

                    //if (attempt < 2)
                    //{
                    //    throw new System.Net.Http.HttpRequestException("Overloaded");
                    //}

                    var result = await client.Messages.GetClaudeMessageAsync(_modelParams);
                    // add the claude answer to message history too
                    _messageHistory.Add(new Message(RoleType.Assistant, result.Message.ToString()));
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

                    return "Claude is currently overloaded. Please try again later.";
                }
                catch (Exception ex)
                {
                    return "An unexpected error occurred: " + ex.Message;
                }

            }
            return "Oopsie daisies the C# is really a dummy";


        }


        // =====================================================================
        // UTILITIES
        // =====================================================================
        public void SessionCleaning()
        {
            _messageHistory.Clear();

        }
    }
}
