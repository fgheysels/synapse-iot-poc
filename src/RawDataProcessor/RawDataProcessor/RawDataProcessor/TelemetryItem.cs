using System;

namespace RawDataProcessor
{
    internal class TelemetryItem
    {
        public DateTimeOffset EnqueuedTimeUtc { get; set; }
        public TelemetryMessage Body { get; set; }
    }
}