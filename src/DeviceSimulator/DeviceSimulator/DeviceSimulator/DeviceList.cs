using System;
using System.Collections.Generic;
using System.Text;

namespace DeviceSimulator
{
    static class DeviceList
    {
        private static readonly Random _randomizer = new Random();

        private static readonly string[] _deviceIds = new[] { "device1", "device2", "device3" };

        public static string PickRandomDeviceId()
        {
            return _deviceIds[_randomizer.Next(_deviceIds.Length - 1)];
        }
    }
}
