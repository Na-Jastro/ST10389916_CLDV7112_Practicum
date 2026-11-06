// Real-time processing infrastructure (requirements a-i)
@description('Base name used to build resource names.')
param appName string = 'practicum-st10389916'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Azure SQL administrator login name.')
param sqlAdminLogin string = 'sqladminuser'

@description('Azure SQL administrator password.')
@secure()
param sqlAdminPassword string

var webPlanName = 'plan-${appName}'
var webAppName = 'app-${appName}'
var functionsPlanName = 'planfunc-${appName}'
var functionsAppName = 'func-${appName}'
var storageName = toLower(replace('st${appName}', '-', ''))
var blobContainerName = 'drone-images'
var telemetryTableName = 'TelemetryData'
var auditTableName = 'UploadAudit'
var irrigationQueueName = 'irrigation-commands'
var sqlServerName = 'sql-${appName}'
var sqlDbName = 'RealtimeProcessing'
var logAnalyticsName = 'log-${appName}'
var appInsightsName = 'appi-${appName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
	sku: {
	  name: 'PerGB2018'
	}
	retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
	Application_Type: 'web'
	WorkspaceResourceId: logAnalytics.id
  }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
	administratorLogin: sqlAdminLogin
	administratorLoginPassword: sqlAdminPassword
	minimalTlsVersion: '1.2'
	publicNetworkAccess: 'Enabled'
  }
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storage.name}/default/${blobContainerName}'
}

resource telemetryTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: '${storage.name}/default/${telemetryTableName}'
}

resource auditTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  name: '${storage.name}/default/${auditTableName}'
}

resource irrigationQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  name: '${storage.name}/default/${irrigationQueueName}'
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
	name: 'S0'
	tier: 'Standard'
  }
  properties: {
	collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
	startIpAddress: '0.0.0.0'
	endIpAddress: '0.0.0.0'
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDbName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource webPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: webPlanName
  location: location
  sku: {
	name: 'S1'
	tier: 'Standard'
  }
  kind: 'linux'
  properties: {
	reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  identity: {
	type: 'SystemAssigned'
  }
  properties: {
	serverFarmId: webPlan.id
	httpsOnly: true
	siteConfig: {
	  linuxFxVersion: 'DOTNETCORE|10.0'
	  appSettings: [
		{
		  name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
		  value: appInsights.properties.ConnectionString
		}
		{
		  name: 'Storage__ConnectionString'
		  value: storageConnectionString
		}
		{
		  name: 'Storage__BlobContainer'
		  value: blobContainerName
		}
		{
		  name: 'Functions__BaseUrl'
		  value: 'https://${functionsAppName}.azurewebsites.net/api'
		}
		{
		  name: 'ConnectionStrings__Sql'
		  value: sqlConnectionString
		}
	  ]
	}
  }
}

resource appServiceAutoscale 'Microsoft.Insights/autoscalesettings@2022-10-01' = {
  name: 'autoscale-${webPlanName}'
  location: location
  properties: {
	enabled: true
	targetResourceUri: webPlan.id
	profiles: [
	  {
		name: 'cpu-and-requests'
		capacity: {
		  minimum: '1'
		  maximum: '5'
		  default: '1'
		}
		rules: [
		  {
			metricTrigger: {
			  metricName: 'CpuPercentage'
			  metricResourceUri: webPlan.id
			  timeGrain: 'PT1M'
			  statistic: 'Average'
			  timeWindow: 'PT5M'
			  timeAggregation: 'Average'
			  operator: 'GreaterThan'
			  threshold: 70
			}
			scaleAction: {
			  direction: 'Increase'
			  type: 'ChangeCount'
			  value: '1'
			  cooldown: 'PT5M'
			}
		  }
		  {
			metricTrigger: {
				metricName: 'HttpQueueLength'
			  metricResourceUri: webPlan.id
			  timeGrain: 'PT1M'
				statistic: 'Count'
			  timeWindow: 'PT5M'
				timeAggregation: 'Average'
			  operator: 'GreaterThan'
				threshold: 100
			}
			scaleAction: {
			  direction: 'Increase'
			  type: 'ChangeCount'
			  value: '1'
			  cooldown: 'PT5M'
			}
		  }
		  {
			metricTrigger: {
			  metricName: 'CpuPercentage'
			  metricResourceUri: webPlan.id
			  timeGrain: 'PT1M'
			  statistic: 'Average'
			  timeWindow: 'PT10M'
			  timeAggregation: 'Average'
			  operator: 'LessThan'
			  threshold: 30
			}
			scaleAction: {
			  direction: 'Decrease'
			  type: 'ChangeCount'
			  value: '1'
			  cooldown: 'PT10M'
			}
		  }
		]
	  }
	]
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: {
	name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
	minimumTlsVersion: 'TLS1_2'
  }
}

resource functionsPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: functionsPlanName
  location: location
  sku: {
	name: 'Y1'
	tier: 'Dynamic'
  }
	kind: 'functionapp'
	properties: {}
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource functionsApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionsAppName
  location: location
	kind: 'functionapp'
  identity: {
	type: 'SystemAssigned'
  }
  properties: {
	serverFarmId: functionsPlan.id
	httpsOnly: true
	siteConfig: {
	  appSettings: [
		{
		  name: 'FUNCTIONS_EXTENSION_VERSION'
		  value: '~4'
		}
		{
		  name: 'FUNCTIONS_WORKER_RUNTIME'
		  value: 'dotnet-isolated'
		}
		{
		  name: 'AzureWebJobsStorage'
		  value: storageConnectionString
		}
		{
		  name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
		  value: storageConnectionString
		}
		{
		  name: 'WEBSITE_CONTENTSHARE'
		  value: toLower(functionsAppName)
		}
		{
		  name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
		  value: appInsights.properties.ConnectionString
		}
		{
		  name: 'StorageConnection'
		  value: storageConnectionString
		}
		{
		  name: 'BlobContainerName'
		  value: blobContainerName
		}
		{
		  name: 'TelemetryTableName'
		  value: telemetryTableName
		}
		{
		  name: 'AuditTableName'
		  value: auditTableName
		}
		{
		  name: 'IrrigationQueueName'
		  value: irrigationQueueName
		}
		{
		  name: 'IrrigationQueueConnection'
		  value: storageConnectionString
		}
		{
		  name: 'SqlConnectionString'
		  value: sqlConnectionString
		}
	  ]
	}
  }
}

var readerRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'acdd72a7-3385-48ef-bd42-f606fba81ae7')

resource readerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, webApp.id, readerRoleId)
  scope: resourceGroup()
  properties: {
	principalId: webApp.identity.principalId
	roleDefinitionId: readerRoleId
	principalType: 'ServicePrincipal'
  }
}

output webAppName string = webApp.name
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output functionsAppName string = functionsApp.name
output functionsPlanName string = functionsPlan.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storage.name
output blobContainerName string = blobContainerName
output telemetryTableName string = telemetryTableName
output irrigationQueueName string = irrigationQueueName
