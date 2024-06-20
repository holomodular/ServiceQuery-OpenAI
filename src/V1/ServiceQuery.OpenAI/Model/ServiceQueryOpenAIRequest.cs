using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServiceQuery.OpenAI
{
    public class ServiceQueryOpenAIRequest
    {
        public ServiceQueryOpenAIRequest()
        {
            ModelName = ServiceQueryOpenAIConstants.DEFAULT_MODELNAME;
            MaxCalls = ServiceQueryOpenAIConstants.DEFAULT_MAXCALLS;
        }

        public string Prompt { get; set; }

        public OpenAIClient OpenAIClient { get; set; }

        public string ModelName { get; set; }
        public int MaxCalls { get; set; }
    }
}