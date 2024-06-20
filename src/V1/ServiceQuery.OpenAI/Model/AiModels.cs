using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceQuery.OpenAI
{
    public class AiServiceQueryFunctionRequest
    {
        public AiServiceQueryObjectRequest sqr { get; set; }
    }

    public class AiServiceQueryObjectRequest : ServiceQueryRequest
    {
        public string objectname { get; set; }
    }

    public class AiSQLDataReq
    {
        public AiSQLData sqstart { get; set; }
    }

    public class AiSQLData
    {
        public string sql { get; set; }
        public int tablecount { get; set; }
    }
}