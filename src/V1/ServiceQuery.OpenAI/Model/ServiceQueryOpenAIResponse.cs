using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceQuery.OpenAI
{
    public class ServiceQueryOpenAIResponse
    {
        public ServiceQueryOpenAIResponse()
        {
            ChatToolCalls = new List<ChatToolCall>();
            Completions = new List<ChatCompletion>();
        }

        public bool Error { get; set; }
        public Exception Exception { get; set; }
        public List<ChatToolCall> ChatToolCalls { get; set; }
        public List<ChatCompletion> Completions { get; set; }
    }
}