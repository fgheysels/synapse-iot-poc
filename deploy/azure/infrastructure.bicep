targetScope = 'resourceGroup'

param location string = 'westeurope'
param iothub_name string = 'fg-datalakepoc-iothub'
param iothub_sku_capacity int = 1
param iothub_sku_name string = 'F1'

param storageaccount_datalake_name string = 'fgdatalakepocstorage'
param storageaccount_datalake_sku_name string = 'Standard_RAGRS'

param rawdataprocessorfunctionapp_name  string = 'fg-datalakepoc-rawdataprocessor'

resource iothub 'Microsoft.Devices/IotHubs@2021-07-01' = {
  name: iothub_name
  location: location
  tags: {    
  }
  sku: {
    capacity: iothub_sku_capacity
    name: iothub_sku_name
  }  
  properties: {   
    messagingEndpoints: {}    
    
    routing: {
      endpoints: {
        
        storageContainers: [
          {
            authenticationType: 'keyBased'
            batchFrequencyInSeconds: 100
            connectionString: 'DefaultEndpointsProtocol=https;AccountName=${telemetrydatalake.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(telemetrydatalake.id, telemetrydatalake.apiVersion).keys[0].value}'
            containerName: 'climateboxes-rawdata'
            encoding: 'JSON'            
            fileNameFormat: 'year={YYYY}/month={MM}/day={DD}/hour={HH}/minute={mm}/{iothub}_{partition}_{YYYY}{MM}{DD}{HH}{mm}.json'            
            maxChunkSizeInBytes: 104857600
            name: 'climateboxes-rawdata'
            resourceGroup: resourceGroup().name
            
          }
        ]
      }
      
      // fallbackRoute: {
      //   condition: 'string'
      //   endpointNames: [ 'string' ]
      //   isEnabled: bool
      //   name: 'string'
      //   source: 'string'
      // }
      routes: [
        {
           condition: 'true'
           endpointNames: [ 
             'climateboxes-rawdata' 
           ]
           isEnabled: true
           name: 'climateboxes-rawdata-route'
           source: 'DeviceMessages'
        }
      ]
    }      
  }   
  dependsOn: [
    telemetrydatalake    
    // rawdataContainer
  ]
} // End of IoT IotHubs

resource telemetrydatalake 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageaccount_datalake_name
  location: location
  tags: {    
  }
  sku: {
    name: storageaccount_datalake_sku_name
  }
  kind: 'StorageV2'  
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: true            
    isHnsEnabled: true    
    minimumTlsVersion: 'TLS1_2'
  }  

  resource blobSvc 'blobServices' = {
    name: 'default'   // Always has value 'default'

     resource rawdataContainer 'containers@2021-04-01' = {    
       name: 'climateboxes-rawdata'      
     }      
  }
  
  // resource rawdataContainer 'containers@2021-04-01'  = {    
  //   name: 'climateboxes-rawdata'      
  // }
}

// resource rawdataContainer 'Microsoft.Storage/storageAccounts/containers@2021-04-01'  = {
//    parent: telemetrydatalake
//    name: 'climateboxes-rawdata'      
// }

resource rawdataprocessorfunctionapp_storageaccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: 'fgdatalakeprocfunstorage'
  location: location
  tags: {    
  }
  sku: {
    name: 'Standard_RAGRS'
  }
  kind: 'StorageV2'  
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: true            
    isHnsEnabled: true    
    minimumTlsVersion: 'TLS1_2'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: 'fg-datalakeprocessing-ai'
  location: location
  kind: 'web'
  properties: { 
    Application_Type: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    // circular dependency means we can't reference functionApp directly  /subscriptions/<subscriptionId>/resourceGroups/<rg-name>/providers/Microsoft.Web/sites/<appName>"
    // 'hidden-link:/subscriptions/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.Web/sites/${functionAppName}': 'Resource'
  }
}

resource rawdataprocessorfunctionapp_hostingplan 'Microsoft.Web/serverfarms@2020-10-01' = {
  name: 'fgdatalakeprocessor-plan'
  location: location
  sku: {
    name: 'Y1' 
    tier: 'Dynamic'
  }
  properties:{
    reserved: true
  }
}

resource rawdataprocessorfunctionapp 'Microsoft.Web/sites@2021-02-01' = {
  name: rawdataprocessorfunctionapp_name
  location: location  
  kind: 'functionapp,linux'  
  identity: {
    type: 'SystemAssigned'    
  }
  properties: {      
    httpsOnly: true    
    serverFarmId: rawdataprocessorfunctionapp_hostingplan.id
    siteConfig: {      
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${rawdataprocessorfunctionapp_storageaccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(rawdataprocessorfunctionapp_storageaccount.id, rawdataprocessorfunctionapp_storageaccount.apiVersion).keys[0].value}'
        }
        {
          'name': 'FUNCTIONS_EXTENSION_VERSION'
          'value': '~3'
        }
        {
          'name': 'FUNCTIONS_WORKER_RUNTIME'
          'value': 'dotnet'
        }   
        {
          'name': 'APPINSIGHTS_INSTRUMENTATIONKEY'
          'value': appInsights.properties.InstrumentationKey
        }     
      ]
      autoHealEnabled: false     
      }
      
      // azureStorageAccounts: {}
      // connectionStrings: [
      //   {
      //     connectionString: 'string'
      //     name: 'string'
      //     type: 'string'
      //   }
      // ]      
  }
  dependsOn: [
    rawdataprocessorfunctionapp_storageaccount
    rawdataprocessorfunctionapp_hostingplan
    appInsights
  ]
}