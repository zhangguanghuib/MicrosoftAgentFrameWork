//YouTube video that cover this sample
//- Basic: https://youtu.be/gJTodKpv8Ik
//- Advanced: https://youtu.be/dCtojrK8bKk
//- MCP: https://youtu.be/Y5IKdt9vdJM

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using Shared;
using System.ClientModel;

using OpenAI.Chat;
using ToolCalling.Basics;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

Secrets secrets = SecretsManager.GetSecrets();

OpenAIClient client = ClientHelper.GetAzureOpenAIClient();

//ChatClientAgent agent = client
//    .GetChatClient("gpt-4.1")
//    .AsAIAgent(
//        instructions: "You are a Time Expert",
//        tools:
//        [
//            AIFunctionFactory.Create(Tools.CurrentDataAndTime, "current_date_and_time"),
//            AIFunctionFactory.Create(Tools.CurrentTimezone, "current_timezone")
//        ]
//    );

#pragma warning disable OPENAI001 // GetResponsesClient is preview API
ChatClientAgent agent = client
    .GetChatClient("gpt-6-sol")
    .AsAIAgent(
        new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Tools =
                [
                    AIFunctionFactory.Create(Tools.CurrentDataAndTime, "current_date_and_time"),
                    AIFunctionFactory.Create(Tools.CurrentTimezone, "current_timezone")
                ],
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.None
                }
            }
        }
    );
#pragma warning restore OPENAI001

AgentSession session = await agent.CreateSessionAsync();

while (true)
{
    Console.Write("> ");
    string? input = Console.ReadLine();
    ChatMessage message = new(ChatRole.User, input);
    AgentResponse response = await agent.RunAsync(message, session);
    Console.WriteLine(response);

    Utils.Separator();
}