using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Claude client specific
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;



namespace ChatVisual
{
    internal class ClaudeClient
    {
        private static readonly string apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? throw new InvalidOperationException("ANTHROPIC_API_KEY is not set.");
        private List<Message> messageHistory;

        // model parameter
        private MessageParameters modelParams;

        // initialize the client
        AnthropicClient client;

        // the constructor for ClaudeClient
        public ClaudeClient()
        {
            // then we need to initialize the AnthropicClient
            client = new AnthropicClient(apiKey);
            messageHistory = new List<Message>();

            // model paramter
            modelParams = new MessageParameters()
            {
                Messages = messageHistory,
                MaxTokens = 1024,
                Model = AnthropicModels.Claude46Sonnet,
                Stream = false
            };

        }

        // then we build the async sendMessage method
        // the method would return string
        public async Task<string> sendMessage(string message, List<String> screenShots)
        {
            Console.WriteLine("Message Send: ", message);

            // we add our current message to our messageHistory list
            if (screenShots.Count == 0)
            {
                Console.WriteLine("Message sent without screenshot");
                messageHistory.Add(new Message(RoleType.User, message));

            } else
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

                messageHistory.Add(new Message()
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

                    var result = await client.Messages.GetClaudeMessageAsync(modelParams);
                    // add the claude answer to message history too
                    messageHistory.Add(new Message(RoleType.Assistant, result.Message.ToString()));
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

    }
}
