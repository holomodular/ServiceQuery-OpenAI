using ServiceQuery.OpenAI;

namespace TestConsoleApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Create query service
            QueryService queryService = new QueryService();

            // Setup request defaults
            ServiceQueryOpenAIRequest request = new ServiceQueryOpenAIRequest()
            {
                MaxCalls = 6,
                ModelName = "gpt-3.5-turbo",
                OpenAIClient = new OpenAI.OpenAIClient("sk-1234567890abcdef1234567890abcdef"), // Put your key here
            };

            Console.WriteLine("ServiceQuery.OpenAI Test Console App");
            Console.WriteLine(ServiceQueryOpenAIConstants.MESSAGE_TABLES_AVAILABLE + queryService.GetTableDefinition(queryService.GetQueryableObjects()));
            Console.WriteLine(Environment.NewLine);

            while (true)
            {
                Console.WriteLine("Enter a query to execute: ");

                // Get input in request
                string input = Console.ReadLine();
                request.Prompt = input;
                ServiceQueryOpenAIResponse response = null;

                // Get AI response (exceptions trapped)
                response = queryService.GetChatResponse(request);

                if (false) // View debug completions
                {
                    // The last completion will be the final response
                    if (response.Completions.Count > 0)
                    {
                        for (int i = 0; i < response.Completions.Count; i++)
                        {
                            var completion = response.Completions[i];
                            Console.WriteLine($"STEP {i + 1}:");
                            Console.WriteLine($"REASON: {completion.FinishReason}");
                            if (completion.Content != null && completion.Content.Count > 0)
                                Console.WriteLine($"CONTENT: {completion.Content[0].Text}");
                            if (completion.ToolCalls != null && completion.ToolCalls.Count > 0)
                            {
                                Console.WriteLine($"TOOLCALL NAME: {completion.ToolCalls[0].FunctionName}");
                                Console.WriteLine($"TOOLCALL ARGS: {completion.ToolCalls[0].FunctionArguments}");
                            }
                            Console.WriteLine(Environment.NewLine);
                        }
                    }
                }
                else if (response.Completions.Count > 0)
                {
                    var choice = response.Completions[response.Completions.Count - 1];
                    if (choice.Content != null && choice.Content.Count > 0)
                        Console.WriteLine(choice.Content[0].Text);
                    else
                        Console.WriteLine("No text response.");
                }

                // Let the user know if an error occurred
                if (response.Error)
                {
                    Console.WriteLine($"Error: {response.Exception.Message}");
                    Console.WriteLine(Environment.NewLine);
                }

                Console.WriteLine(Environment.NewLine + Environment.NewLine);
            }
        }
    }
}