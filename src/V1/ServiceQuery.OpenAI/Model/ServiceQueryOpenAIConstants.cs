using System;
using System.Collections.Generic;
using System.Text;

namespace ServiceQuery.OpenAI
{
    public class ServiceQueryOpenAIConstants
    {
        public const string DEFAULT_MODELNAME = "gpt-3.5-turbo-0125";
        public const int DEFAULT_MAXCALLS = 10;
        public const string APPSETTING_OPTIONS = "ServiceQueryOpenAI";

        public const string TOOL_START = "start";
        public const string TOOL_QUERY = "query";

        public const string MESSAGE_INTRO_FIRST = @"
Act like a database administrator that can create SQL queries.
";

        public const string MESSAGE_INTRO_SECOND = @"
I want you to create a SQL query using the tables/objects listed above.
Do not use the 'AS' keyword in the query.
Do not use functions in the select statement.
Do not use joins between tables/objects.
If you need to join tables/objects together, you need to use a sub-query using primary keys.
You can only use one table/object at a time in a sub-query.
If there are multiple steps, call the first query to get the list of ids needed for the second query and merge the results and so on.
";

        public static string MESSAGE_TABLES_AVAILABLE = @"
The following objects/tables are available, including their property names and .net c# datatypes enclosed in parenthesis:
";

        public static string MESSAGE_MANAGER = @"
Break the original query down into a series of sql statements for each individual object/table to get the data you need.
Start with the innermost query and work your way out.
The results of previous sql statement will be used in subsequent function calls to rebuild the queries listed in the steps needed to get the data.
Each query can only query one object/table at a time and you can't use joins between objects or use the 'as' keyword.
List out all the steps in order to get the data for the following request.
";

        public static string MESSAGE_TASKS = @"
Create multiple function calls in order for the following tasks.
The results you receive will be used to rebuild the next query/function call that needs to be made.
When you receive all function call results, evaluate them to the original request and determine if another function call is needed.
If not, answer the original request.
";

        public static string MESSAGE_CONVERSION = @"
Act like a database administrator and software developer and convert a SQL query into a JSON object-based request to call a function.
The following are examples on how to convert a query given an employee object with the properties: employeeid (int), createdate (DateTimeOffset), age (int?), firstname (string), lastname (string), email (string), title (string), isactive (bool)
Boolean properties are stored as strings in the database and you should use true and false instead of 1 and 0.
Ex: SELECT * FROM employee
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[] } }
Ex: SELECT employeeid, firstname, lastname FROM employee
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""select"", ""properties"":[""employeeid"",""firstname"",""lastname""]}] }
Ex: SELECT COUNT(employeeid) FROM employee WHERE isactive = 1
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""count"", ""properties"":[""employeeid""]}, { ""filterType"":""equal"", ""properties"":[""isactive""], ""values"":[""true""]}]} }
Ex: SELECT COUNT(DISTINCT firstname) FROM employee
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""count"", ""properties"":[""firstname""]}, { ""filterType"":""select"", ""properties"":[""firstname""] }, { ""filterType"":""distinct"" }]} }
Ex: SELECT MAX(createdate) FROM employee
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""maximum"", ""properties"":[""createdate""]}]} }
Ex: SELECT MIN(createdate) FROM employee
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""minimum"", ""properties"":[""createdate""]}]} }
Ex: SELECT SUM(age) FROM employee WHERE age is not null
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""sum"", ""properties"":[""age""]},{ ""filterType"":""isnotnull"", ""properties"":[""age""]}]} }
Ex: SELECT * FROM employee WHERE firstname = 'john'
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""equal"", ""properties"":[""firstname""], ""values"":[""john""]}]} }
Ex: SELECT * FROM employee WHERE firstname like '%john%' AND lastname like 'smith%' AND age > 30
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[{ ""filterType"":""contains"", ""properties"":[""firstname""], ""values"":[""john""]},{ ""filterType"":""and""},{ ""filterType"":""startswith"", ""properties"":[""lastname""], ""values"":[""smith""]},{ ""filterType"":""and""},{ ""filterType"":""greaterthan"", ""properties"":[""age""], ""values"":[""30""]}]} }
Ex: SELECT * FROM employee WHERE employeeid > 10 AND ( lastname = ""smith"" OR lastname like ""%doe"" )
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""greaterthan"", ""properties"":[""employeeid""], ""values"":[""10""] },{ ""filterType"":""and""},{ ""filterType"":""begin""},{ ""filterType"":""equal"", ""properties"":[""lastname""], ""values"":[""smith""] },{ ""filterType"":""or""},{ ""filterType"":""endswith"", ""properties"":[""lastname""], ""values"":[""doe""] },{ ""filterType"":""end""}]} }
Ex: SELECT * FROM employee WHERE employeeid in (7,9,11)
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""inset"", ""properties"":[""employeeid""], ""values"":[""7"",""9"",""11""] }]} }
Ex: SELECT * FROM employee WHERE employeeid not in (63, 2)
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""notinset"", ""properties"":[""employeeid""], ""values"":[""63"",""2""] }]} }
Ex: SELECT * FROM employee WHERE age between (18,40)
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""between"", ""properties"":[""age""], ""values"":[""18"",""40""] }]} }
Ex: SELECT * FROM employee WHERE age >= 21
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""greaterthanorequal"", ""properties"":[""age""], ""values"":[""21""] }]} }
Ex: SELECT * FROM employee WHERE age < 50
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""lessthan"", ""properties"":[""age""], ""values"":[""50""] }]} }
Ex: SELECT * FROM employee WHERE age <= 50 ORDER BY firstname ASC
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""lessthanorequal"", ""properties"":[""age""], ""values"":[""50""] },{ ""filterType"":""sortasc"", ""properties"":[""firstname""] }]} }
ExL SELECT * FROM employee WHERE age is null
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""isnull"", ""properties"":[""age""] }]} }
Ex: SELECT * FROM employee WHERE age is not null ORDER BY lastname DESC
{ ""sqr"":{ ""objectname"":""employee"", ""filters"":[ { ""filterType"":""isnotnull"", ""properties"":[""age""] },{ ""filterType"":""sortdesc"", ""properties"":[""lastname""] }]} }
";

        public const string CONVERSION_ORIGINAL_PREFIX = @"
The original request is: ";

        public const string CONVERSION_TASKS_PREFIX = @"
The tasks are: ";

        public const string CONVERSION_TASKS_NOMANAGER = @"
The tasks is to call the query function with the converted query.";
    }
}