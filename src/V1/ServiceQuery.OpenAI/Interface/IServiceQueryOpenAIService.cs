using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServiceQuery.OpenAI
{
    public interface IServiceQueryOpenAIService
    {
        ServiceQueryOpenAIResponse GetChatResponse(ServiceQueryOpenAIRequest request);

        string GetTableDefinition(List<IQueryable> queryables);

        List<IQueryable> GetQueryableObjects();
    }
}