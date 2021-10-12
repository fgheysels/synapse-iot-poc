using System;

namespace RawDataProcessor
{
    public class TelemetryItem
    {
        public DateTimeOffset EnqueuedTimeUtc { get; set; }
        public TelemetryMessage Body { get; set; }
    }
}