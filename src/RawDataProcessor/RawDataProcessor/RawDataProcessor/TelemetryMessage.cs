using System;

namespace RawDataProcessor
{
    internal class TelemetryMessage
    {
        public string DeviceId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Metric[] Metrics { get; set; }
    }

    internal class Metric
    {
        public string Tag { get; set; }
        public object Value { get; set; }
    }
}