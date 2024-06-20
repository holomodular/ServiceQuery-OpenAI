using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceQuery;
using ServiceQuery.OpenAI;
using Newtonsoft.Json;

namespace TestConsoleApp
{
    public class QueryService : ServiceQueryOpenAIService
    {
        private readonly IQueryable<EmployeeTable> employees = EmployeeTable.GetEmployees().AsQueryable();

        /// <summary>
        /// Override this method to get the queryable objects. This is used to get the list of objects and properties for the table definition.
        /// </summary>
        /// <returns></returns>
        public override List<IQueryable> GetQueryableObjects()
        {
            return new List<IQueryable>()
            {
                // You can add your database tables here or mock objects, they are actually queried in function below.
                // databaseContext.Employees.AsQueryable(),
                employees, // This is just a list of objects
            };
        }

        /// <summary>
        /// Override this method to get the ServiceQueryResponse data serialized to a string.
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        protected override string GetServiceQueryResponseJsonData(string objectName, ServiceQueryRequest request)
        {
            if (string.Compare(objectName, employees.ElementType.Name, true) == 0)
            {
                var serviceQueryResponse = request.GetServiceQuery().Execute(employees);
                return JsonConvert.SerializeObject(serviceQueryResponse);
            }
            return string.Empty;
        }
    }
}