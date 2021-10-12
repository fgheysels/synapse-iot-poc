using System;
using System.Collections.Generic;
using System.Text;

namespace DeviceSimulator
{
    class TelemetryMessage
    {
        public string DeviceId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Metric[] Metrics { get; set; }
    }

    class Metric
    {
        public string Tag { get; set; }
        public object Value { get; set; }
    }
}
