using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace RawDataProcessor
{
    public class Settings
    {
        private static readonly string ConfigurationContainerName = "rawdataprocessor-configuration";
        private static readonly string SettingsFileName = "settings.json";

        /// <summary>
        /// Gets the date/time of the last processed telemetry-item.
        /// </summary>
        public DateTimeOffset LastProcessingDate { get; set; }

        public static async Task<Settings> GetSettingsAsync(string storageConnectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference(ConfigurationContainerName);

            await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, new BlobRequestOptions(), new OperationContext());
            var blob = container.GetBlobReference(SettingsFileName);

            if (await blob.ExistsAsync())
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(ms);

                    ms.Position = 0;

                    var content = ms.ToArray();
                    var json = System.Text.Encoding.UTF8.GetString(content);

                    return JsonConvert.DeserializeObject<Settings>(json);
                }
            }

            return new Settings
            {
                LastProcessingDate = DateTimeOffset.UtcNow.AddDays(-1)
            };
        }

        public async Task SaveSettingsAsync(string storageConnectionString)
        {
            BlobServiceClient serviceClient = new BlobServiceClient(storageConnectionString);
            
            var containerClient = serviceClient.GetBlobContainerClient(ConfigurationContainerName);

            using (var ms = new MemoryStream())
            {
                var json = JsonConvert.SerializeObject(this);
                ms.Write(System.Text.Encoding.UTF8.GetBytes(json));
                ms.Position = 0;
                var blobClient = containerClient.GetBlobClient(SettingsFileName);
                await blobClient.UploadAsync(ms, overwrite: true);
            }
        }
    }
}