﻿using System;
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

        private static readonly TagDefinition[] _tagDefinitions = new[] {
            new TagDefinition("temp", 5, 22),
            new TagDefinition("humidity", 35,80),
            new TagDefinition("size", 1, 170),
            new TagDefinition("flux", 0.5, 4.5)
        };

        private static readonly Random _randomizer = new Random();

        public static Metric GenerateRandomMetric()
        {
            var tag = _tagDefinitions[_randomizer.Next(_tagDefinitions.Length - 1)];

            return new Metric
            {
                Tag = tag.TagName,
                Value = _randomizer.NextDouble() * (tag.MaxValue - tag.MinValue) + tag.MinValue
            };
        }

        public static Metric[] GenerateRandomMetrics()
        {
            var metrics = new Dictionary<string, Metric>();

            int numberOfMetrics = _randomizer.Next(1, _tagDefinitions.Length - 1);

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