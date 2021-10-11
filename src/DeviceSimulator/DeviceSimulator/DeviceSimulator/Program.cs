using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;

namespace DeviceSimulator
{
    class Program
    {
        private const string DeviceConnectionString = "HostName=fg-datalake-poc.azure-devices.net;DeviceId=temperaturesimulator;SharedAccessKey=6wkbPu7QMPGuCGFruSH0RScPUAQ5JANYw77Xf+fvslw=";

        private static bool _stopApplication = false;

        static async Task Main(string[] args)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            Console.WriteLine("Connecting to IoT Hub ...");

            DeviceClient client = DeviceClient.CreateFromConnectionString(DeviceConnectionString);

            await client.OpenAsync();

            Console.WriteLine("Connected! Sending messages to IoT Hub, CTRL+C to stop.");

            while (!_stopApplication)
            {
                TelemetryMessage telemetry = GenerateSampleTelemetry();

                Message deviceMessage = CreateDeviceMessageForTelemetryMessage(telemetry);

                await client.SendEventAsync(deviceMessage);

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            Console.WriteLine("Stopping...");

            await client.CloseAsync();

            Console.WriteLine("Stopped.");
        }

        private static Message CreateDeviceMessageForTelemetryMessage(TelemetryMessage telemetry)
        {
            var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(telemetry)));

            // Setting ContentType and ContentEncoding to these values is required to be able to
            // store messages as JSON in Azure storage via IoT Hub routing.
            message.ContentType = "application/json";
            message.ContentEncoding = "UTF-8";

            return message;
        }

        private static TelemetryMessage GenerateSampleTelemetry()
        {
            var message = new TelemetryMessage()
            {
                Timestamp = DateTimeOffset.UtcNow,
                DeviceId = DeviceList.PickRandomDeviceId(),
                Metrics = MetricList.GenerateRandomMetrics()
            };

            return message;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _stopApplication = true;
            e.Cancel = true;
        }
    }
}
