using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parquet;
using Parquet.Data;
using Parquet.Data.Rows;

namespace RawDataProcessor
{
    internal class TelemetryParquetWriter
    {
        /// <summary>
        /// Converts the TelemetryItem collection into a Parquet file format.
        /// </summary>
        /// <param name="items"></param>
        /// <returns>A collection of <see cref="ParquetContent"/> instances.  Each instance contains an identifier and a Stream that contains the parquet-contents.</returns>
        internal IEnumerable<ParquetContent> CreateParquetContents(IEnumerable<TelemetryItem> items)
        {
            // First, group the metrics on DeviceId, since we're going to write files per DeviceId and per Timestamp
            var groups = GroupTelemetry(items);

            foreach (var deviceTelemetry in groups)
            {
                var dateGroups = deviceTelemetry.ToLookup(t => t.Timestamp.DayOfYear);

                foreach (var dayTelemetry in dateGroups)
                {
                    var stream = CreateParquetContentForGroup(dayTelemetry);
                    yield return new ParquetContent($"telemetry_{deviceTelemetry.Key}_{dayTelemetry.Key}.parquet", stream);
                }
            }
        }

        private static Stream CreateParquetContentForGroup(IGrouping<int, TelemetryMessage> dayTelemetry)
        {
            // Define Parquet Schema first:
            // 1st column = deviceId
            // 2nd column = timestamp
            // For every Metric, we define a (double) column.
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
            var stream = new MemoryStream();

            using (var parquetWriter = new ParquetWriter(schema, stream))
            {
                // Parquet is a column-based format, so it allows you to define a column and write all contents
                // for that column at once.
                // However, in this case our information is row-based.  Therefore, define a a table and populate
                // it with the appropriate contents and write the table at once.

                Table t = new Table(schema);

                foreach (var telemetry in dayTelemetry)
                {
                    List<object> values = new List<object>();
                    values.Add(telemetry.DeviceId);
                    values.Add(telemetry.Timestamp);

                    // Populate each column for this row with the appropriate values. If we do not have a value
                    // for that column, null must be added.

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

            return stream;
        }

        private static ILookup<string, TelemetryMessage> GroupTelemetry(IEnumerable<TelemetryItem> items)
        {
            var orderedItems = items.OrderBy(i => i.Body.DeviceId).ThenBy(i => i.Body.Timestamp);

            return orderedItems.ToLookup(x => x.Body.DeviceId, b => b.Body);
        }
    }

    public class ParquetContent
    {
        public string Identifier { get; }
        public Stream Content { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParquetContent"/> class.
        /// </summary>
        public ParquetContent(string identifier, Stream content)
        {
            Identifier = identifier;
            Content = content;
            Content.Position = 0;
        }
    }
}