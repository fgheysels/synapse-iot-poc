targetScope = 'resourceGroup'

param location string = 'westeurope'
param iothub_name string = 'fg-datalakepoc-iothub'
param iothub_sku_capacity int = 1
param iothub_sku_name string = 'F1'

param datalake_name string = 'fgdatalakepocstorage'
param datalake_sku_name string = 'Standard_RAGRS'

param functionapp_name  string = 'fg-datalakepoc-rawdataprocessor'

param functionapp_storageaccount_name string = 'fgdatalakeprocfunstorage'

param functionapp_hostingplan_name  string = 'fg-datalakeprocessing-ai'

param appinsights_name string = 'fg-datalakepoc-ai'

param synapse_name string = 'fg-datalakepoc-synapse'
param synapse_managedresourcegroup_name string = '${resourceGroup().name}-managed'
param synapse_admin_username string = 'synapseadmin'
@secure()
param synapse_admin_password string

param keyvault_name string = 'fg-datalakepoc-keyvault'

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
            fileNameFormat: 'year={YYYY}/month={YYYY}{MM}/date={YYYY}{MM}{DD}/{iothub}_{partition}_{YYYY}{MM}{DD}{HH}{mm}.json'            
            maxChunkSizeInBytes: 104857600
            name: 'climateboxes-rawdata'
            resourceGroup: resourceGroup().name            
          }
        ]
      }      
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
  ]
} 

resource telemetrydatalake 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: datalake_name
  location: location
  tags: {    
  }
  sku: {
    name: datalake_sku_name
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
      properties:{
        publicAccess: 'Container'
      }   
    }   
    resource parquetstorageContainer 'containers@2021-04-01' = {    
      name: 'parquet-contents'      
      properties:{
        publicAccess: 'Container'
      }   
    }      
  }  
}

resource rawdataprocessorfunctionapp_storageaccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: functionapp_storageaccount_name
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
  name: appinsights_name
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
  name: functionapp_hostingplan_name
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
  name: functionapp_name
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
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }   
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }     
        {
          name: 'SettingsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${rawdataprocessorfunctionapp_storageaccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(rawdataprocessorfunctionapp_storageaccount.id, rawdataprocessorfunctionapp_storageaccount.apiVersion).keys[0].value}'
        }
        {
          name: 'RawTelemetryConnectionString'
          value: ''
        }
        {
          name: 'ParquetStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${telemetrydatalake.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(telemetrydatalake.id, telemetrydatalake.apiVersion).keys[0].value}'
        }
      ]
      autoHealEnabled: false     
      }    
  }
  dependsOn: [
    rawdataprocessorfunctionapp_storageaccount
    rawdataprocessorfunctionapp_hostingplan
    appInsights
  ]
}

resource synapse 'Microsoft.Synapse/workspaces@2021-06-01-preview' = {
  name: synapse_name
  location: location  
  identity: {
    type: 'SystemAssigned'    
  }
  properties: {
    connectivityEndpoints: {}    
    defaultDataLakeStorage: {
      accountUrl: telemetrydatalake.properties.primaryEndpoints.dfs
      filesystem: 'filesystem'
    }
    encryption: {      
    }
    managedResourceGroupName: synapse_managedresourcegroup_name    
    sqlAdministratorLogin: synapse_admin_username
    sqlAdministratorLoginPassword: synapse_admin_password
  }
  dependsOn: [
    telemetrydatalake
  ]
}

resource synapseFirewallRuleAllowAll 'Microsoft.Synapse/workspaces/firewallRules@2021-06-01-preview' = {
  name: '${synapse.name}/allowAll'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: keyvault_name
  location: location
  tags: {    
  }
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }    
    enableSoftDelete: false
    accessPolicies: []
    tenantId: subscription().tenantId
  }

  resource synapseadmin_password 'secrets@2021-06-01-preview' = {
    name: 'SynapseAdminPassword'
    tags: {    
    }
    properties: {
      attributes: {
        enabled: true      
      }
      contentType: 'string'
      value: synapse_admin_password
    }
  }
}

var storageBlobDataContributorRoleID = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource datalake_roleassignment_functionapp 'Microsoft.Authorization/roleAssignments@2021-04-01-preview' = {
  name: guid(telemetrydatalake.id, rawdataprocessorfunctionapp.id, storageBlobDataContributorRoleID)
  scope: telemetrydatalake
  properties: {      
    principalId: rawdataprocessorfunctionapp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleID)
  }
  dependsOn:[
    rawdataprocessorfunctionapp
    telemetrydatalake
  ]
}
