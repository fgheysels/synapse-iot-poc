# Introduction

This project is a PoC to see how we can ingest received telemetry data directly into a Data Lake from IoT Hub, and query that data using Azure Synapse.

## Getting started

First of all, some Azure resources must be created.  This project contains a bicep file which describes all the resources that are required.  This file can be found in the `deploy\azure` folder.

Create the required Azure resources by executing the following statement:

```azcli
az deployment group create --subscription <subscriptionid> --resource-group <resourcegroupname> --template-file infrastructure.bicep --name <deploymentname> --parameters <parameters>
```

some help

select top 10 *
from openrowset(
        bulk 'https://fgdatalakepocstorage.blob.core.windows.net/rawdata/fg-datalake-poc/01/*/*/*/*/*.json',
        format = 'csv',
        fieldterminator ='0x0b',
        fieldquote = '0x0b'
    ) with (doc nvarchar(max)) as row


    https://devblogs.microsoft.com/azure-sql/read-azure-storage-files-using-synapse-sql-external-tables/

    