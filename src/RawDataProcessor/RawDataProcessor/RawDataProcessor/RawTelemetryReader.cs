using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace RawDataProcessor
{
    internal class RawTelemetryReader
    {
        private readonly AzureServiceTokenProvider _tokenProvider = new AzureServiceTokenProvider();
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the <see cref="RawTelemetryReader"/> class.
        /// </summary>
        internal RawTelemetryReader(string connectionString)
        {
            _connectionString = connectionString;
        }
        
        internal async Task< IEnumerable<TelemetryItem>> ReadRawTelemetryRecordsSinceAsync(DateTimeOffset fromDate)
        {
            var telemetryItems = new List<TelemetryItem>();

            using (var connection = new SqlConnection(_connectionString))
            {
                var token = await _tokenProvider.GetAccessTokenAsync("https://database.windows.net/");

                connection.AccessToken = token;
                connection.Open();

                string query = "SELECT * \n" +
                               "FROM telemetrydata \n" +
                               "WHERE YEAR >= @p_Year AND Month >= @p_Month AND Day >= @p_Day \n" +
                               "AND Hour >= @p_Hour AND Minute >= @p_Minute \n" +
                               "AND JSON_VALUE(doc, '$.EnqueuedTimeUtc') > @p_LastRunDate \n" +
                               "ORDER BY JSON_VALUE(doc, '$.EnqueuedTimeUtc')";

                var command = new SqlCommand(query);
                command.Connection = connection;
                command.Parameters.Add("@p_Year", SqlDbType.Int).Value = fromDate.Year;
                command.Parameters.Add("@p_Month", SqlDbType.Int).Value = fromDate.Month;
                command.Parameters.Add("@p_Day", SqlDbType.Int).Value = fromDate.Day;
                command.Parameters.Add("@p_Hour", SqlDbType.Int).Value = fromDate.Hour;
                command.Parameters.Add("@p_Minute", SqlDbType.Int).Value = fromDate.Minute;
                command.Parameters.Add("@p_LastRunDate", SqlDbType.DateTimeOffset).Value = fromDate;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        var item = JsonConvert.DeserializeObject<TelemetryItem>(Convert.ToString(reader[0]));
                        telemetryItems.Add(item);
                    }

                    reader.Close();
                }
            }

            return telemetryItems;
        }
    }
}