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
        public DateTimeOffset LastRunDate { get; set; }

        public static async Task<Settings> GetSettingsAsync(string storageConnectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();

            var container = blobClient.GetContainerReference("rawdataprocessor-configuration");

            await container.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Blob, new BlobRequestOptions(), new OperationContext());
            var blob = container.GetBlobReference("settings.json");

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
            else
            {
                return new Settings
                {
                    LastRunDate = new DateTimeOffset(2021, 9, 1, 0, 0, 0, TimeSpan.FromHours(0))
                };
            }
        }

        public async Task SaveSettingsAsync(string storageConnectionString)
        {
            BlobServiceClient serviceClient = new BlobServiceClient(storageConnectionString);
            
            var containerClient = serviceClient.GetBlobContainerClient("rawdataprocessor-configuration");

            using (var ms = new MemoryStream())
            {
                var json = JsonConvert.SerializeObject(this);
                ms.Write(System.Text.Encoding.UTF8.GetBytes(json));
                ms.Position = 0;
                var blobClient = containerClient.GetBlobClient("settings.json");
                await blobClient.UploadAsync(ms, overwrite: true);
            }
        }
    }
}