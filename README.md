# SmartHotel360
During **Connect(); 2017** event this year we presented beautiful app demos using Xamarin and many features of Azure. For //build/ 2018's keynote, we updated some components of the back-end API code to support **Azure Kubernetes Service (AKS)**. This repository contains the setup instructions and sample code needed for the Internet of Things (IoT) Demo utilizing Digital Twins that was announced at Ignite 2018.

# SmartHotel360 Repos
For this reference app scenario, we built several consumer and line-of-business apps and an Azure backend. You can find all SmartHotel360 repos in the following locations:

* [SmartHotel360](https://github.com/Microsoft/SmartHotel360)
* [IoT Demo](https://github.com/Microsoft/SmartHotel360-IoT)
* [Backend Services (optimized for Kubernetes)](https://github.com/Microsoft/SmartHotel360-AKS-DevSpaces-Demo)
* [Public Website](https://github.com/Microsoft/SmartHotel360-public-web)
* [Pet Checker Serverless Function](https://github.com/Microsoft/SmartHotel360-PetCheckerFunction)
* [Mobile Apps](https://github.com/Microsoft/SmartHotel360-mobile-desktop-apps)
* [Sentiment Analysis](https://github.com/Microsoft/SmartHotel360-Sentiment-Analysis-App)
* [Migrating Internal apps to Azure](https://github.com/Microsoft/SmartHotel360-internal-booking-apps)
* [Original Backend Services](https://github.com/Microsoft/SmartHotel360-Azure-backend)

# SmartHotel360 - IoT Demo

Welcome to the SmartHotel360 IoT repository. Here you'll find everything you need to run the website and backend for the IoT demo.

![IoT Demo Architecture Diagram](docs/Architecture.png "IoT Demo Architecture")

## Getting Started

SmartHotel360 uses a **microservice oriented** architecture implemented using containers. For the IoT Demo, there are various services developed in different technologies: .NET Core 2, Asp.Net Core 2, and Angular. These services use different Azure Resources like Digital Twins, App Services, Cosmos DB, Event Hubs, Azure Functions, and IoT Hubs to name a few. The step by step walkthrough for creating and provisioning the demo can be found next in the [Setup](#Setup) section.

End-to-end setup takes about an hour provided you have all of the development environment prerequisites met AND that your user has the required permissions in your Azure subscription.

# Setup

## Prerequisites

* Powershell for running provisioning Azure provisioning scripts. [Powershell Core](https://docs.microsoft.com/en-us/powershell/scripting/setup/installing-powershell-core-on-macos?view=powershell-6) is available for Mac and Linux users.
* [Docker](http://www.docker.com) to build the containers.
* [Visual Studio 2017](https://www.visualstudio.com) with the `.NET desktop development` and  `ASP.NET and web development` workloads installed.
* [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest)
* [Kubernetes CLI](https://kubernetes.io/docs/tasks/tools/install-kubectl/)
* [Angular CLI](https://cli.angular.io/)
* **Required Azure Subscription Permissions**
  1. Create and modify Azure AD Application Registrations or have someone else do it for you
  2. Create a new Service Principal for Role Based Access Control (RBAC) or have someone else do it for you
  3. Create or add 2 new guest users to the Active Directory (AD) or have someone else do it for you and get you the Object Ids of those users
  4. Create a ResourceGroup or have one that someone else already created for you to use
  5. Create a deployment into a ResourceGroup
  6. Give the Service Principal in #2 read permissions to read from the ACR created by the deployment (if not, then script provides the user with an Azure Cli command to execute to be run by someone with the required permission, and pauses until the user says this step has been completed)

## Set up a Service Principal and register an Azure Active Directory application

 Follow these instructions to [create a service principal and register an Azure Active Directory application](https://docs.microsoft.com/en-us/azure/azure-resource-manager/resource-group-create-service-principal-portal?view=azure-cli-latest).

During the creation process you will need to take note of the following information:

* Tenant Id
* App Id
* App Key

### Obtain the service principal Id
To obtain the service principal Id, open a **Powershell** window and follow these steps:
1. `Login-AzureRmAccount -SubscriptionId {subcription id}`
2. `Get-AzureRmADServicePrincipal -ApplicationId {app Id}`

### Set permissions and security for the Application

Click settings --> Required Permissions
* Click Add on the top left
* Select the API you'd like to use to get external data into Digital Twins (e.g. Microsoft Graph)
* Select the permissions that your app needs to have in order to access the correct information
* Select Save, and then click Add again
* Select the "Azure Smart Spaces Service" API
* Check the Read/Write Access delegated permissions box
* Save, and select Grant Permissions

## Create a service principal for AKS Cluster
To create a service principal for an AKS Cluster, open a **Powershell/Command Prompt/Bash** window and follow these steps:
1. `az login`
2. `az account set -s {subscription id}`
3. `az ad sp create-for-rbac --skip-assignment`

The `sp create-for-rbac` command will return a json object that will have the information needed later in this process.

```json
{
  "appId": "{aks app id}",
  "displayName": "{aks display name}",
  "name": "{aks uri}",
  "password": "{aks service principal password}",
  "tenant": "{aks tenant id}"
}
```

## Create IoT Demo users
You need to create two users having access to your AAD. These can either be users created in your AAD or guest users that you add. For both of these users, you need to collect the Object Id for them. One user will be used for the **Manager** role and the other the **Employee** role.
* To get the Object Id, view the user in AAD and you will see **Object ID** under the **Identity Section**. This is a similar process to using the [Admin Center of a subscription backed by an Office 365 environment](http://blog.schertz.name/2018/06/locating-ids-in-azure-ad/).

## Provision resources in Azure
In the `/arm/` folder of this repository is the deployment script to create and stand up all of the resources to run this demo in Azure. To execute the deployment script, run the following in a **Powershell** window:

```powershell
.\deploy.ps1 -subscriptionId {subscription id} -resourceGroupName {resource group name} -resourceGroupLocation {resource group location} -managerObjId {manager object id} -employeeObjId {employee object id} -clientId {app id} -clientSecret {app key} -clientServicePrincipalId {service principal id} -aksServicePrincipalId {AKS Service Principal App Id} -aksServicePrincipalKey {AKS Service Principal password}
```

The following information parameters are required for the deployment script:
* `{subscription id}`: the Azure subscription id that has been used in this setup
* `{resource group name}`: the resource group name to add the created resource, if it doesn't exist it will be created
* `{resource group location}`: location for the resource group: (ie. 'westus2')
* `{manager object id}`: object Id obtained when creating **Manager** user [above](#Create-IoT-Demo-users)
* `{employee object id}`: object Id obtained when creating **Employee** user [above](#Create-IoT-Demo-users)
* `{app id}`: app id obtained from [creating an AAD app](#Set-up-a-Service-Principal-and-register-an-Azure-Active-Directory-application)
* `{app key}`: app key obtained from [creating an AAD app](#Set-up-a-Service-Principal-and-register-an-Azure-Active-Directory-application)
* `{service principal id}`: service principal id from [above](#Obtain-the-service-principal-Id)
* `{AKS service principal app id}`: AKS app id from [above](#Create-a-service-principal-for-AKS-Cluster)
* `{AKS service principal password}`: AKS service principal password from [above](#Create-a-service-principal-for-AKS-Cluster)

There is a collection of parameters that are used by the deployment script that determine properties like resource location, pricing tiers, and naming. If you wish to modify any of these parameters you can edit the values in [parameters.json](./arm/parameters.json).

## User Settings
When the deployment script is complete, it will output a `userSettings.json` file with information needed for the rest of the deployment.

```json
{
    "tenantId":  "{tenant id}",
    "clientId":  "{client id}",
    "aadReplyUrl":  "{facility management web reply uri for ADAL}",
    "digitalTwinsManagementEndpoint": "{digital twins management endpoint}",
    "facilityManagementWebsiteUri": "url to the facility management website",
    "facilityManagementApiUri":  "{facility management api}",
    "facilityManagementApiEndpoint":  "{facility management api endpoint}",
    "storageConnectionString": "{connection string to azure storage account}",
    "eventHubConsumerConnectionString": "{consumer connection string to the event hub}",
    "iotHubConnectionString": "{connection string to the iot hub}",
    "cosmosDbConnectionString": "{connection string to the cosmos db}",
    "roomDevicesApiEndpoint":  "{room devices api endpoint - needed for running the Xamarin Mobile Clients}",
    "room11SpaceId":  "{room 11 space id - needed for running the Xamarin Mobile Clients}"
}
```

**NOTE:** If you are going to run the IoT Demo for the [Xamarin Mobile Apps](https://github.com/Microsoft/SmartHotel360-mobile-desktop-apps#iot-demo), then you will need the `roomDevicesApiEndpoint` and `room11SpaceId` values from the `userSettings.json` file.

## Success!
To verify that everything is working correctly, open up the `facilityManagementWebsiteUri` (from the `userSettings.json` in the browser and log in with one of the two users created during the provisioning steps.

# Running Locally (OPTIONAL)
Portions of this demo can be run locally. In order to do so, you still **must complete** the [Setup](#setup) section. Once you have completed the provisioning, you can follow these steps to configure, build, and deploy the resource locally.

## Azure Function
The Sensor Data function can run locally in Visual Studio. However, in order to avoid conflicts, you must first disable the Azure Function that was created during [Setup](#Setup).

1. Log in to the [Azure Portal](https://portal.azure.com).
2. Navigate to **Function Apps** and select the Azure Function that was created during [Setup](#Setup).
3. Click the **Stop** button to disable the Azure Function.
4. Open the `/backend/src/SmartHotel.Services/SmartHotel.Services.sln` solution in Visual Studio 2017
5. Set the `SmartHotel.Services.SensorDataFunction` project as the startup project
6. Right-click the `SmartHotel.Services.SensorDataFunction` project in the **Solution Explorer**
7. Add a new json file named: `local.settings.json` and set the contents to this:
    ```json
    {
        "IsEncrypted": false,
        "Values": {
            "AzureWebJobsStorage": "{storage connection string}",
            "AzureWebJobsDashboard": "{storage connection string}",
            "FUNCTIONS_WORKER_RUNTIME": "dotnet",
            "EventHubConnectionString": "{event hub consumer connection string}",
            "CosmosDBConnectionString": "{cosmos db connection string}",
        }
    }
    ```
    * {storage connection string}: `storageConnectionString` from the **userSettings.json** file from the [User Settings](#User-Settings) section
    * {event hub consumer connection string}: `eventHubConsumerConnectionString` from the **userSettings.json** file from  the [User Settings](#User-Settings) section
    * {cosmos db connection string}: `cosmosDbConnectionString` from the **userSettings.json** file from  the [User Settings](#User-Settings) section
8. Run in debug mode.

## IoT Devices
While running the devices locally, we need to remove the instances that exist in the Kubernetes cluster. Run the following steps from a **Powershell/Command Prompt/Bash** window:

1. `kubectl delete pods,deployments,replicasets --all` - this removes ALL pods from the cluster, including the Room Devices Api pod.
2. If NOT running the Room Devices Api locally, then re-deploy that pod to the cluster:
   * Change directory to `/backend/src/SmartHotel.Services`
   * `kubectl apply -f deployments.demo.yaml`

To redeploy the devices to the cluster:
* Change directory to `/backend/src/SmartHotel.Devices`
* `kubectl apply -f deployments.demo.yaml`

## APIs
The Apis can be run locally at the same time as their Azure counterparts.

### Running via Docker
From a **Powershell/Command Prompt/Bash** window

1. Change directory to `/backend/src/SmartHotel.Services`
2. `docker-compose build` - only required if you're making changes and want to see them.
3. `docker-compose -f docker-compose.yml -f docker-compose.override.yml up`

## Running via Visual Studio
1. Open the `/backend/src/SmartHotel.Services/SmartHotel.Services.sln` solution in Visual Studio 2017
2. Select either the `SmartHotel.Services.FacilityManagement` or `SmartHotel.Services.FacilityManagement` project as the startup project
3. Update the `appsettings.json` file.
   * SmartHotel.Services.FacilityManagement:
     * ManagementApiUrl: `digitalTwinsManagementEndpoint` from the **userSettings.json** file from  the [User Settings](#User-Settings) section
     * MongoDBConnectionString: `cosmosDbConnectionString` from the **userSettings.json** file from  the [User Settings](#User-Settings) section
   * SmartHotel.Services.RoomDevicesApi:
     * IoTHubConnectionString: `iotHubConnectionString` from the **userSettings.json** file from  the [User Settings](#User-Settings) section
     * DatabaseConnectionString: `cosmosDbConnectionString` from the **userSettings.json** file from  the [User Settings](#User-Settings) section
4. Run in debug mode.

## Website
The Website can be run locally at the same time as its Azure counterpart.

1. Open the `/FacilityManagementWebsite/SmartHotel.FacilityManagementWeb/SmartHotel.FacilityManagementWeb.sln` solution in Visual Studio 2017
2. If you desire the website to connect to the Facility Management API in Azure, then run in debug mode.
3. Otherwise, update the `environment.ts` and `environment.prod.ts` files under `/SmartHotel.FacilityManagementWeb/ClientApp/src/environments`:

   ```javascript
   export const environment = {
     production: true,
     version: 'Production',
     sensorDataTimer: {sensorDataTime},
     adalConfig: {
       tenant: '{tenantId}',
       clientId: '{clientId}',
       endpoints: {
         '{apiUri}': '{clientId}'
       }
     } as adal.Config,
     apiEndpoint: '{apiEndpoint}',
     resourceId: '0b07f429-9f4b-4714-9392-cc5e8e80c8b0'
   };
   ```

   * {apiUri}: Must be updated to point to wherever your local Facility Management Api is running (e.g. `http://localhost:3000`)
   * {apiEndpoint}: Must be updated to point to wherever your local Facility Management Api is running, but append `/api` (e.g. `http://localhost:3000/api`)
4. Then run in debug mode.

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
