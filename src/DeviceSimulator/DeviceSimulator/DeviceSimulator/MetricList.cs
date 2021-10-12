using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeviceSimulator
{
    static class MetricList
    {
        private class TagDefinition
        {
            public string TagName { get; }
            public double MinValue { get; }
            public double MaxValue { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="TagDefinition"/> class.
            /// </summary>
            public TagDefinition(string tagName, double minValue, double maxValue)
            {
                TagName = tagName;
                MinValue = minValue;
                MaxValue = maxValue;
            }
        }

        private static readonly TagDefinition[] TagDefinitions = new[] {
            new TagDefinition("temp", -5, 22),
            new TagDefinition("humidity", 35,80),
            new TagDefinition("size", 1, 170),
            new TagDefinition("flux", 0.5, 4.5),
            new TagDefinition("current", 0, 3.5),
            new TagDefinition("voltage", 0, 110),
            new TagDefinition("ph", 4, 12),
            new TagDefinition("lumen", 200, 3000)
        };

        private static readonly Random Randomizer = new Random();

        public static Metric GenerateRandomMetric()
        {
            var tag = TagDefinitions[Randomizer.Next(TagDefinitions.Length - 1)];

            return new Metric
            {
                Tag = tag.TagName,
                Value = Randomizer.NextDouble() * (tag.MaxValue - tag.MinValue) + tag.MinValue
            };
        }

        public static Metric[] GenerateRandomMetrics()
        {
            var metrics = new Dictionary<string, Metric>();

            int numberOfMetrics = Randomizer.Next(1, TagDefinitions.Length - 1);

            while (metrics.Count < numberOfMetrics)
            {
                var metric = GenerateRandomMetric();

                if (metrics.ContainsKey(metric.Tag) == false)
                {
                    metrics.Add(metric.Tag, metric);
                }
            }

            return metrics.Values.ToArray();
        }
    }
}
