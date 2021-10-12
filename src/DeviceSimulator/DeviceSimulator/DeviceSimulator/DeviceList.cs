using System;

namespace DeviceSimulator
{
    static class DeviceList
    {
        private static readonly Random Randomizer = new Random();

        private static readonly string[] DeviceIds = new[] { "device1", "device2", "device3" };

        public static string PickRandomDeviceId()
        {
            return DeviceIds[Randomizer.Next(DeviceIds.Length - 1)];
        }
    }
}
