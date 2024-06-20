using OpenAI.Chat;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;
using OpenAI.Assistants;
using System.Text.Json.Nodes;
using System.Security.AccessControl;
using Newtonsoft.Json;
using Azure.Core;
using Azure;

namespace ServiceQuery.OpenAI
{
    public abstract class ServiceQueryOpenAIService : IServiceQueryOpenAIService
    {
        /// <summary>
        ///  Override this method to get the queryable objects. This is used to get the list of objects and properties for the table definition.
        /// </summary>
        /// <returns></returns>
        public abstract List<IQueryable> GetQueryableObjects();

        /// <summary>
        ///  Override this method to get the ServiceQueryResponse data serialized to a string.
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        protected abstract string GetServiceQueryResponseJsonData(string objectName, ServiceQueryRequest request);

        /// <summary>
        /// Get a response for the given input request.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <exception cref="ServiceQueryException"></exception>
        public ServiceQueryOpenAIResponse GetChatResponse(ServiceQueryOpenAIRequest request)
        {
            ServiceQueryOpenAIResponse response = new ServiceQueryOpenAIResponse();
            try
            {
                // Validations
                if (request == null)
                    throw new ServiceQueryException("Request is null.");
                if (string.IsNullOrEmpty(request.Prompt))
                    throw new ServiceQueryException("Prompt is null or empty.");

                // Start the process
                return StartWorkflow(request, response);
            }
            catch (Exception ex)
            {
                response.Error = true;
                response.Exception = ex;
            }
            return response;
        }

        /// <summary>
        /// Override this method to change the table definition
        /// </summary>
        /// <param name="queryables"></param>
        /// <returns></returns>
        /// <exception cref="ServiceQueryException"></exception>
        public virtual string GetTableDefinition(List<IQueryable> queryables)
        {
            string tableDefinition = string.Empty;
            foreach (var queryable in queryables)
            {
                if (!string.IsNullOrEmpty(tableDefinition))
                    tableDefinition += Environment.NewLine;

                var type = queryable.ElementType;
                var tprops = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (tprops == null || tprops.Length == 0)
                    throw new ServiceQueryException($"Queryable {type} has no properties.");

                List<PropertyInfo> props = new List<PropertyInfo>(tprops);
                tableDefinition += $"{type.Name} - " + string.Join(",", props.Select(p => p.Name + " (" + p.PropertyType.ToString() + ")").ToList());
            }
            return tableDefinition;
        }

        /// <summary>
        /// Override this method to audit the chat completion.
        /// </summary>
        /// <param name="openAIClient"></param>
        /// <param name="modelName"></param>
        /// <param name="messages"></param>
        /// <returns></returns>
        protected virtual ChatCompletion GetChatCompletion(OpenAIClient openAIClient, string modelName, List<ChatMessage> messages, List<ChatTool> tools)
        {
            ChatCompletionOptions options = new ChatCompletionOptions();
            if (tools != null)
            {
                foreach (var tool in tools)
                    options.Tools.Add(tool);
            }
            var chatClient = openAIClient.GetChatClient(modelName);
            return chatClient.CompleteChat(messages, options).Value;
        }

        private ServiceQueryOpenAIResponse StartWorkflow(ServiceQueryOpenAIRequest request, ServiceQueryOpenAIResponse response)
        {
            // Start Intro
            var introCompletion = GetIntroRequest(request);
            if (introCompletion == null)
                throw new ServiceQueryException("Intro Completion is null.");

            // Save response
            response.Completions.Add(introCompletion);
            if (response.Completions.Count >= request.MaxCalls)
                return response;

            // Make sure it is a tool/function call
            if (introCompletion.FinishReason == ChatFinishReason.FunctionCall ||
               introCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                if (introCompletion.ToolCalls != null && introCompletion.ToolCalls.Count > 0)
                {
                    // Get tool call arguments
                    var introCall = introCompletion.ToolCalls[0];
                    var introArguments = JsonConvert.DeserializeObject<AiSQLDataReq>(introCall.FunctionArguments);
                    if (string.Compare(introCall.FunctionName, ServiceQueryOpenAIConstants.TOOL_START, true) == 0)
                    {
                        if (introArguments != null && introArguments.sqstart != null && !string.IsNullOrEmpty(introArguments.sqstart.sql))
                        {
                            // If more than one table, break down into individual queries
                            if (introArguments.sqstart.tablecount > 1)
                                StartManagerWorkflow(request, response, introArguments);
                            else
                                StartConversionWorkflow(request, response, null);
                        }
                    }
                }
            }
            return response;
        }

        private void StartManagerWorkflow(ServiceQueryOpenAIRequest request, ServiceQueryOpenAIResponse response, AiSQLDataReq args)
        {
            // Start Manager
            ChatCompletion managerCompletion = GetManagerRequest(request, args);
            if (managerCompletion == null)
                throw new ServiceQueryException("Manager Completion is null.");

            // Save response
            response.Completions.Add(managerCompletion);
            if (response.Completions.Count >= request.MaxCalls)
                return;

            // Make sure it is text response
            if (managerCompletion.Content != null && managerCompletion.Content.Count > 0)
            {
                var managerResponse = managerCompletion.Content[0];
                if (!string.IsNullOrEmpty(managerResponse.Text))
                    StartConversionWorkflow(request, response, managerResponse.Text);
            }
        }

        private void StartConversionWorkflow(ServiceQueryOpenAIRequest request, ServiceQueryOpenAIResponse response, string managerResponse)
        {
            // Start Conversion
            List<ChatMessage> messages = GetConversionChatMessages(request, managerResponse);
            List<ChatTool> tools = new List<ChatTool>()
            {
                GetQueryTool()
            };
            var conversionCompletion = GetChatCompletion(request.OpenAIClient, request.ModelName, messages, tools);
            if (conversionCompletion == null)
                throw new ServiceQueryException("Conversion Completion is null.");

            // Save response
            response.Completions.Add(conversionCompletion);
            if (response.Completions.Count >= request.MaxCalls)
                return;

            // Make sure it is a tool/function call
            if (conversionCompletion.FinishReason == ChatFinishReason.FunctionCall ||
               conversionCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                if (conversionCompletion.ToolCalls != null && conversionCompletion.ToolCalls.Count > 0)
                {
                    // Start Recursive Querying Calls
                    var managerCall = conversionCompletion.ToolCalls[0];
                    if (string.Compare(managerCall.FunctionName, ServiceQueryOpenAIConstants.TOOL_QUERY, true) == 0)
                        StartRecursiveQuery(request, response, managerCall, messages);
                }
            }
        }

        private void StartRecursiveQuery(ServiceQueryOpenAIRequest request, ServiceQueryOpenAIResponse response, ChatToolCall chatToolCall, List<ChatMessage> messages)
        {
            var queryArguments = JsonConvert.DeserializeObject<AiServiceQueryFunctionRequest>(chatToolCall.FunctionArguments);
            if (queryArguments != null && queryArguments.sqr != null && !string.IsNullOrEmpty(queryArguments.sqr.objectname))
            {
                // Get Data
                string responseData = GetServiceQueryResponseJsonData(queryArguments.sqr.objectname, queryArguments.sqr);
                messages.Add(new AssistantChatMessage(new List<ChatToolCall>() { chatToolCall }));
                messages.Add(new ToolChatMessage(chatToolCall.Id, responseData));

                // Send data response back
                List<ChatTool> tools = new List<ChatTool>()
                {
                    GetQueryTool()
                };
                var completion = GetChatCompletion(request.OpenAIClient, request.ModelName, messages, tools);
                response.Completions.Add(completion);
                if (response.Completions.Count >= request.MaxCalls)
                    return;

                // Make sure it is a tool/function call
                if (completion.FinishReason == ChatFinishReason.FunctionCall ||
                   completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    if (completion.ToolCalls != null && completion.ToolCalls.Count > 0)
                    {
                        // Start Recursion
                        var subsequentCall = completion.ToolCalls[0];
                        if (string.Compare(subsequentCall.FunctionName, ServiceQueryOpenAIConstants.TOOL_QUERY, true) == 0)
                            StartRecursiveQuery(request, response, subsequentCall, messages);
                    }
                }
            }
        }

        private ChatCompletion GetManagerRequest(ServiceQueryOpenAIRequest request, AiSQLDataReq introArguments)
        {
            string tableDefinition = GetTableDefinition(GetQueryableObjects());
            string systemMessage =
                ServiceQueryOpenAIConstants.MESSAGE_TABLES_AVAILABLE +
                tableDefinition +
                ServiceQueryOpenAIConstants.MESSAGE_MANAGER;

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(introArguments.sqstart.sql),
            };
            return GetChatCompletion(request.OpenAIClient, request.ModelName, messages, null);
        }

        private List<ChatMessage> GetConversionChatMessages(ServiceQueryOpenAIRequest request, string managerResponse)
        {
            string tableDefinition = GetTableDefinition(GetQueryableObjects());
            string systemMessage =
                ServiceQueryOpenAIConstants.MESSAGE_CONVERSION +
                ServiceQueryOpenAIConstants.MESSAGE_TABLES_AVAILABLE +
                tableDefinition +
            ServiceQueryOpenAIConstants.MESSAGE_TASKS;
            var messages = new List<ChatMessage>()
                {
                    new SystemChatMessage(systemMessage),
                    new UserChatMessage(ServiceQueryOpenAIConstants.CONVERSION_ORIGINAL_PREFIX + request.Prompt),
                };

            if (!string.IsNullOrEmpty(managerResponse))
                messages.Add(new AssistantChatMessage(ServiceQueryOpenAIConstants.CONVERSION_TASKS_PREFIX + managerResponse));
            else
                messages.Add(new AssistantChatMessage(ServiceQueryOpenAIConstants.CONVERSION_TASKS_NOMANAGER));
            return messages;
        }

        private ChatCompletion GetIntroRequest(ServiceQueryOpenAIRequest request)
        {
            var queryables = GetQueryableObjects();
            if (queryables == null || queryables.Count == 0)
                throw new ServiceQueryException("Queryable objects are null or empty.");

            string tableDefinition = GetTableDefinition(queryables);

            string systemPrompt =
                ServiceQueryOpenAIConstants.MESSAGE_INTRO_FIRST +
                ServiceQueryOpenAIConstants.MESSAGE_TABLES_AVAILABLE +
                tableDefinition +
                ServiceQueryOpenAIConstants.MESSAGE_INTRO_SECOND;

            List<ChatMessage> messages = new List<ChatMessage>()
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(request.Prompt),
                };

            var tool = GetIntroTool();

            return GetChatCompletion(request.OpenAIClient, request.ModelName, messages, new List<ChatTool>() { tool });
        }

        private ChatTool GetIntroTool()
        {
            var binaryData = BinaryData.FromObjectAsJson(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["sqstart"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["sql"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = @"the sql statement"
                            },
                            ["tablecount"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = @"the integer number of tables used in the query"
                            }
                        },
                        ["required"] = new JsonArray { "sql", "tablecount" }
                    }
                },
                ["required"] = new JsonArray { "sqstart" }
            });
            return ChatTool.CreateFunctionTool(ServiceQueryOpenAIConstants.TOOL_START, "start", binaryData);
        }

        private ChatTool GetQueryTool()
        {
            var binaryData = BinaryData.FromObjectAsJson(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["sqr"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["objectname"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = @"The name of the object to query"
                            },
                            ["filters"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["description"] = @"The servicequery filters",
                                ["items"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["filterType"] = new JsonObject
                                        {
                                            ["type"] = "string",
                                            ["description"] = @"The filterType or sql command"
                                        },
                                        ["properties"] = new JsonObject
                                        {
                                            ["type"] = "array",
                                            ["description"] = @"The list of object property names",
                                            ["items"] = new JsonObject
                                            {
                                                ["type"] = "string",
                                            }
                                        },
                                        ["values"] = new JsonObject
                                        {
                                            ["type"] = "string",
                                            ["description"] = @"The list of property values",
                                            ["items"] = new JsonObject
                                            {
                                                ["type"] = "string",
                                            }
                                        }
                                    },
                                    ["required"] = new JsonArray { "filterType" }
                                }
                            }
                        }
                    }
                },
                ["required"] = new JsonArray { "sqr" }
            });
            return ChatTool.CreateFunctionTool(ServiceQueryOpenAIConstants.TOOL_QUERY, "query data", binaryData);
        }
    }
}