using Azure.Storage.Blobs;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Parquet;
using Parquet.Data;
using Parquet.Data.Rows;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RawDataProcessor
{
    public class ProcessTelemetry
    {

        private readonly AzureServiceTokenProvider _serviceTokenProvider = new AzureServiceTokenProvider();
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessTelemetry"/> class.
        /// </summary>
        public ProcessTelemetry(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName(nameof(ProcessTelemetry))]
        public async Task Run(
            [TimerTrigger("0 */2 * * * *", RunOnStartup = false)] TimerInfo myTimer, ILogger log)
        {
            var settings = await Settings.GetSettingsAsync(_configuration["SettingsStorage"]);

            string connectionString = _configuration["RawTelemetryConnectionString"];

            var telemetryItems = new List<TelemetryItem>();

            using (var connection = new SqlConnection(connectionString))
            {
                var token = await _serviceTokenProvider.GetAccessTokenAsync("https://database.windows.net/");

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
                command.Parameters.Add("@p_Year", SqlDbType.Int).Value = settings.LastRunDate.Year;
                command.Parameters.Add("@p_Month", SqlDbType.Int).Value = settings.LastRunDate.Month;
                command.Parameters.Add("@p_Day", SqlDbType.Int).Value = settings.LastRunDate.Day;
                command.Parameters.Add("@p_Hour", SqlDbType.Int).Value = settings.LastRunDate.Hour;
                command.Parameters.Add("@p_Minute", SqlDbType.Int).Value = settings.LastRunDate.Minute;
                command.Parameters.Add("@p_LastRunDate", SqlDbType.DateTimeOffset).Value = settings.LastRunDate;

                var reader = await command.ExecuteReaderAsync();


                while (reader.Read())
                {
                    var line = Convert.ToString(reader[0]);

                    var item = JsonConvert.DeserializeObject<TelemetryItem>(line);
                    telemetryItems.Add(item);
                }

                reader.Close();
            }

            var p = new TelemetryProcessor();
            p.WriteToParquet(telemetryItems);

            var dateOfLastProcessedItem = telemetryItems.Max(t => t.EnqueuedTimeUtc);
            settings.LastRunDate = dateOfLastProcessedItem;
            await settings.SaveSettingsAsync(_configuration["SettingsStorage"]);
        }
    }

    public class TelemetryProcessor
    {
        public void WriteToParquet(IEnumerable<TelemetryItem> items)
        {
            // First, group the metrics on DeviceId, since we're going to write files per DeviceId and per Timestamp
            var groups = GroupTelemetry(items);

            foreach (var deviceTelemetry in groups)
            {
                var dateGroups = deviceTelemetry.ToLookup(t => t.Timestamp.DayOfYear);

                foreach (var dayTelemetry in dateGroups)
                {
                    CreateParquetFile($"telemetry_{deviceTelemetry.Key}_{dayTelemetry.Key}.parquet", dayTelemetry);
                }
            }

        }

        private static void CreateParquetFile(string filename, IGrouping<int, TelemetryMessage> dayTelemetry)
        {
            // Define Parquet Schema first:
            // Column1 = deviceId
            // Column2 = timestamp
            // For every Metric, we define a column.
            var columnDefinitions = new List<DataField>();

            var deviceIdColumn = new DataField("deviceId", DataType.String, hasNulls: false);
            var timestampColumn = new DateTimeDataField("timestamp", DateTimeFormat.DateAndTime, hasNulls: false);

            columnDefinitions.Add(deviceIdColumn);
            columnDefinitions.Add(timestampColumn);

            var uniqueMetricNames = dayTelemetry.SelectMany(x => x.Metrics).Select(m => m.Tag).Distinct().OrderBy(_ => _);

            foreach (var metricName in uniqueMetricNames)
            {
                var metricColumn = new DataField(metricName, DataType.Double, hasNulls: true);
                columnDefinitions.Add(metricColumn);
            }

            var schema = new Schema(columnDefinitions.ToArray());

            // TODO: for perf reasons, it is advised that a rowgroup doesn't exceed 5000 rows.
            using (Stream fs = File.OpenWrite(Path.Combine("c:\\temp\\", filename)))
            {
                using (var parquetWriter = new ParquetWriter(schema, fs))
                {
                    Table t = new Table(schema);

                    foreach (var telemetry in dayTelemetry)
                    {
                        List<object> values = new List<object>();
                        values.Add(telemetry.DeviceId);
                        values.Add(telemetry.Timestamp);

                        for (int i = 2; i < columnDefinitions.Count; i++)
                        {
                            var metric = telemetry.Metrics.FirstOrDefault(m => m.Tag == columnDefinitions[i].Name);
                            if (metric != null)
                            {
                                values.Add(metric.Value);
                            }
                            else
                            {
                                values.Add(null);
                            }
                        }

                        Row r = new Row(values);

                        t.Add(r);
                    }

                    parquetWriter.Write(t);
                }
            }

        }

        private ILookup<string, TelemetryMessage> GroupTelemetry(IEnumerable<TelemetryItem> items)
        {
            var orderedItems = items.OrderBy(i => i.Body.DeviceId).ThenBy(i => i.Body.Timestamp);

            return orderedItems.ToLookup(x => x.Body.DeviceId, b => b.Body);
        }
    }

    public class TelemetryItem
    {
        public DateTimeOffset EnqueuedTimeUtc { get; set; }
        public TelemetryMessage Body { get; set; }
    }

    public class TelemetryMessage
    {
        public string DeviceId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Metric[] Metrics { get; set; }
    }

    public class Metric
    {
        public string Tag { get; set; }
        public object Value { get; set; }
    }

    public class Settings
    {
        public DateTimeOffset LastRunDate { get; set; }

        public static async Task<Settings> GetSettingsAsync(string storageConnectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference("rawdataprocessor-configuration");

            await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, new BlobRequestOptions(), new OperationContext());
            var blob = container.GetBlobReference("settings.json");

            if (await blob.ExistsAsync())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(ms);

                    ms.Position = 0;

                    var content = ms.ToArray();
                    var json = System.Text.Encoding.UTF8.GetString(content);

                    return JsonConvert.DeserializeObject<Settings>(json);
                }
            }
            else
            {
                return new Settings()
                {
                    LastRunDate = new DateTimeOffset(2021, 9, 1, 0, 0, 0, TimeSpan.FromHours(0))
                };
            }
        }

        public async Task SaveSettingsAsync(string storageConnectionString)
        {
            BlobServiceClient serviceClient = new BlobServiceClient(storageConnectionString);

            var containerClient = serviceClient.GetBlobContainerClient("rawdataprocessor-configuration");

            using (var ms = new MemoryStream())
            {
                var json = JsonConvert.SerializeObject(this);
                ms.Write(System.Text.Encoding.UTF8.GetBytes(json));
                ms.Position = 0;
                var blobClient = containerClient.GetBlobClient("settings.json");
                await blobClient.UploadAsync(ms, overwrite: true);
                //await containerClient.UploadBlobAsync("settings.json", ms);
            }



            //CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            //var blobClient = storageAccount.CreateCloudBlobClient();

            //var container = blobClient.GetContainerReference("rawdataprocessor-configuration");


            //var blob = container.GetBlobReference("settings.json");

            //container.

            //if (await blob.ExistsAsync())
            //{
            //    using (MemoryStream ms = new MemoryStream())
            //    {
            //        await blob.DownloadToStreamAsync(ms);

            //        ms.Position = 0;

            //        var content = ms.ToArray();
            //        var json = System.Text.Encoding.UTF8.GetString(content);

            //        return JsonConvert.DeserializeObject<Settings>(json);
            //    }
            //}
        }
    }
}
