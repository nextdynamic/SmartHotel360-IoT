﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using SmartHotel.IoT.Provisioning.Common;
using SmartHotel.IoT.Provisioning.Common.Models;
using SmartHotel.IoT.Provisioning.Common.Models.DigitalTwins;
using Type = SmartHotel.IoT.Provisioning.Common.Models.DigitalTwins.Type;

namespace SmartHotel.IoT.Provisioning
{
	// Following example from https://github.com/Azure-Samples/digital-twins-samples-csharp/tree/master/occupancy-quickstart
	public class Program
	{
		private const string AadInstance = "https://login.microsoftonline.com/";
		private const string DigitalTwinsResourceId = "0b07f429-9f4b-4714-9392-cc5e8e80c8b0";

		private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
		{
			DefaultValueHandling = DefaultValueHandling.Ignore,
			NullValueHandling = NullValueHandling.Ignore
		};

		public static async Task<int> Main( string[] args ) => await CommandLineApplication.ExecuteAsync<Program>( args );

		[Option( "-t|--Tenant", Description = "Azure Tenant Id" )]
		[Required]
		public string Tenant { get; }

		[Option( "-ci|--ClientId", Description = "Azure Active Directory App Id" )]
		[Required]
		public string ClientId { get; }

		[Option( "-cs|--ClientSecret", Description = "Key from the Azure Active Directory App" )]
		[Required]
		public string ClientSecret { get; }

		[Option( "-dt|--DigitalTwinsApiEndpoint", Description = "Url for your Digital Twins resource e.g. (https://{resource name}.{resource location}.azuresmartspaces.net/management/api/v1.0" )]
		[Required]
		public string DigitalTwinsApiEndpoint { get; }

		[Option( "-ehcs|--EventHubConnectionString", Description = "Connection string to the Event Hub" )]
		[Required]
		public string EventHubConnectionString { get; }

		[Option( "-ehscs|--EventHubSecondaryConnectionString", Description = "Secondary Connection string to the Event Hub" )]
		[Required]
		public string EventHubSecondaryConnectionString { get; }

		[Option( "-ehn|--EventHubName", Description = "Name of the Event Hub" )]
		[Required]
		public string EventHubName { get; }

		[Option( "-moid|--ManagerObjectId", Description = "Azure Object Id for the Manager user" )]
		[Required]
		public string ManagerObjectId { get; }

		[Option( "-eoid|--EmployeeObjectId", Description = "Azure Object Id for the Employee user" )]
		[Required]
		public string EmployeeObjectId { get; }

		[Option( "-o|--output", Description = "Name of the file to save provisioning data to.  This is used by SmartHotel.IoT.ProvisioningDevices to configure device settings" )]
		public string OutputFile { get; }

		private async Task OnExecuteAsync()
		{
			HttpClient httpClient = await HttpClientHelper.GetHttpClientAsync( DigitalTwinsApiEndpoint, AadInstance, Tenant,
				DigitalTwinsResourceId, ClientId, ClientSecret );

			ProvisioningDescription provisioningDescription = ProvisioningHelper.LoadSmartHotelProvisioning();

			await CreateSpacesAsync( httpClient, provisioningDescription.spaces, Guid.Empty, Guid.Empty );
			await CreateEndpointsAsync( httpClient, provisioningDescription.endpoints );

			if ( !string.IsNullOrEmpty( OutputFile ) )
			{
				IDictionary<string, List<DeviceDescription>> allDevices = provisioningDescription.spaces.GetAllDeviceDescriptions();

				await File.WriteAllTextAsync( OutputFile, JsonConvert.SerializeObject( allDevices ) );
			}

			Console.WriteLine();
			Console.WriteLine();
		}

		private async Task CreateEndpointsAsync( HttpClient httpClient, IList<EndpointDescription> endpointDescriptions )
		{
			IReadOnlyCollection<Endpoint> existingEndpoints = await EndpointHelpers.GetEndpointsAsync( httpClient, JsonSerializerSettings );
			foreach ( EndpointDescription endpointDescription in endpointDescriptions )
			{
				if ( existingEndpoints.Any( e => e.Type == endpointDescription.type
												 && e.Path == EventHubName
												 && e.EventTypes.Intersect( endpointDescription.eventTypes ).Any() ) )
				{
					// Assuming that if endpoing matching Type, Path, and EventTypes already exists then its connection strings are already correct
					continue;
				}

				Endpoint endpoint = endpointDescription.ToDigitalTwins( EventHubConnectionString, EventHubSecondaryConnectionString, EventHubName );

				Console.WriteLine( $"Creating Endpoint: {JsonConvert.SerializeObject( endpoint, Formatting.Indented, JsonSerializerSettings )}" );
				var request = HttpMethod.Post.CreateRequest( "endpoints", JsonConvert.SerializeObject( endpoint, JsonSerializerSettings ) );
				var response = await httpClient.SendAsync( request );
				if ( !response.IsSuccessStatusCode )
				{
					Console.WriteLine( "Failed to create endpoint." );
				}
			}

			Console.WriteLine();
		}

		private async Task CreateSpacesAsync( HttpClient httpClient, IList<SpaceDescription> spaceDescriptions,
			Guid parentId, Guid keystoreId )
		{
			foreach ( SpaceDescription spaceDescription in spaceDescriptions )
			{
				Space spaceToCreate = spaceDescription.ToDigitalTwins( parentId );
				Space existingSpace = await SpaceHelpers.GetUniqueSpaceAsync( httpClient, spaceToCreate.Name, parentId, JsonSerializerSettings );
				var createdId = !string.IsNullOrWhiteSpace( existingSpace?.Id )
					? Guid.Parse( existingSpace.Id )
					: await CreateSpaceAsync( httpClient, spaceToCreate );

				Console.WriteLine();

				if ( createdId != Guid.Empty )
				{
					Guid keystoreIdToUseForChildren = keystoreId;
					// Keystore creation must happen first to ensure that devices down the tree can get their SaS tokens from it
					if ( !string.IsNullOrWhiteSpace( spaceDescription.keystoreName ) )
					{
						Guid createdKeystoreId = await CreateKeystoreAsync( httpClient, spaceDescription.keystoreName, createdId );
						if ( createdKeystoreId != Guid.Empty )
						{
							keystoreIdToUseForChildren = createdKeystoreId;
						}
					}

					// Resources must be created next to ensure that devices created down the tree will succeed
					if ( spaceDescription.resources != null )
					{
						await CreateResourcesAsync( httpClient, spaceDescription.resources, createdId );
					}

					// Types must be created next to ensure that devices/sensors created down the tree will succeed
					if ( spaceDescription.types != null )
					{
						await CreateTypesAsync( httpClient, spaceDescription.types, createdId );
					}

					if ( spaceDescription.devices != null )
					{
						await CreateDevicesAsync( httpClient, spaceDescription.devices, keystoreIdToUseForChildren, createdId );
					}

					if ( spaceDescription.users != null )
					{
						await CreateUserRoleAssignmentsAsync( httpClient, spaceDescription.users, createdId );
					}

					if ( spaceDescription.spaces != null )
					{
						await CreateSpacesAsync( httpClient, spaceDescription.spaces, createdId, keystoreIdToUseForChildren );
					}
				}
			}
		}

		private static async Task<Guid> CreateSpaceAsync( HttpClient httpClient, Space space )
		{
			Console.WriteLine( $"Creating Space: {JsonConvert.SerializeObject( space, Formatting.Indented, JsonSerializerSettings )}" );
			var request = HttpMethod.Post.CreateRequest( "spaces", JsonConvert.SerializeObject( space, JsonSerializerSettings ) );
			var response = await httpClient.SendAsync( request );
			return await response.GetIdAsync();
		}

		private static async Task<Guid> CreateKeystoreAsync( HttpClient httpClient, string keystoreName, Guid spaceId )
		{
			if ( spaceId == Guid.Empty )
			{
				throw new ArgumentException( $"Keystore must have a {nameof( spaceId )}" );
			}

			Keystore keystore = new Keystore
			{
				Name = keystoreName,
				SpaceId = spaceId.ToString()
			};

			Console.WriteLine( $"Creating Keystore: {JsonConvert.SerializeObject( keystore, Formatting.Indented, JsonSerializerSettings )}" );
			var request = HttpMethod.Post.CreateRequest( "keystores", JsonConvert.SerializeObject( keystore, JsonSerializerSettings ) );
			var response = await httpClient.SendAsync( request );
			Guid keystoreId = await response.GetIdAsync();
			if ( keystoreId == Guid.Empty )
			{
				Console.WriteLine( "Failed to create keystore" );
			}
			else
			{
				Console.WriteLine( "Generating key" );
				var keyRequest = HttpMethod.Post.CreateRequest( $"keystores/{keystoreId}/keys" );
				var keyResponse = await httpClient.SendAsync( keyRequest );
				if ( !keyResponse.IsSuccessStatusCode )
				{
					Console.WriteLine( "Failed generating key." );
				}
				Console.WriteLine();
			}

			return keystoreId;
		}

		private static async Task CreateResourcesAsync( HttpClient httpClient, IList<ResourceDescription> resourceDescriptions,
			Guid spaceId )
		{
			if ( spaceId == Guid.Empty )
			{
				throw new ArgumentException( $"Resources must have a {nameof( spaceId )}" );
			}

			var resourceIds = new List<Guid>();
			foreach ( ResourceDescription resourceDescription in resourceDescriptions )
			{
				Resource resource = resourceDescription.ToDigitalTwins( spaceId );
				Guid createdId = await CreateResourceAsync( httpClient, resource );
				if ( createdId != Guid.Empty )
				{
					resourceIds.Add( createdId );
				}
				else
				{
					Console.WriteLine( $"Failed to create resource. Please try manually: {resourceDescription.type}" );
				}
			}

			if ( resourceIds.Any() )
			{
				// wait until all the resources are created and ready to use in case downstream operations (like device creation)
				//	are dependent on it.
				Console.WriteLine( "Polling until all resources are no longer in the provisioning state." );

				IEnumerable<Task<bool>> statusVerificationTasks =
					resourceIds.Select( resourceId => ResourcesHelpers.WaitTillResourceCreationCompletedAsync( httpClient, resourceId ) );

				await Task.WhenAll( statusVerificationTasks );
			}
		}

		private static async Task<Guid> CreateResourceAsync( HttpClient httpClient, Resource resource )
		{
			Console.WriteLine( $"Creating Resource: {JsonConvert.SerializeObject( resource, Formatting.Indented, JsonSerializerSettings )}" );
			var request = HttpMethod.Post.CreateRequest( "resources", JsonConvert.SerializeObject( resource, JsonSerializerSettings ) );
			var response = await httpClient.SendAsync( request );
			return await response.GetIdAsync();
		}

		private static async Task CreateTypesAsync( HttpClient httpClient, IList<TypeDescription> typeDescriptions, Guid spaceId )
		{
			if ( spaceId == Guid.Empty )
			{
				throw new ArgumentException( $"Types must have a {nameof( spaceId )}" );
			}

			foreach ( TypeDescription typeDescription in typeDescriptions )
			{
				Type type = typeDescription.ToDigitalTwins( spaceId );

				Console.WriteLine( $"Creating Type: {JsonConvert.SerializeObject( type, Formatting.Indented, JsonSerializerSettings )}" );
				var request = HttpMethod.Post.CreateRequest( "types", JsonConvert.SerializeObject( type, JsonSerializerSettings ) );
				await httpClient.SendAsync( request );
			}
		}

		private static async Task CreateDevicesAsync( HttpClient httpClient, IList<DeviceDescription> deviceDescriptions,
			Guid keystoreIdToUseForChildren, Guid spaceId )
		{
			if ( spaceId == Guid.Empty )
			{
				throw new ArgumentException( $"Devices must have a {nameof( spaceId )}" );
			}

			foreach ( DeviceDescription deviceDescription in deviceDescriptions )
			{
				deviceDescription.SpaceId = spaceId;
				Device device = deviceDescription.ToDigitalTwins();

				Device existingDevice = await deviceDescription.GetSingleExistingDeviceAsync( httpClient, JsonSerializerSettings );
				Guid createdId = !string.IsNullOrWhiteSpace( existingDevice?.Id )
					? Guid.Parse( existingDevice.Id )
					: await device.CreateDeviceAsync( httpClient, JsonSerializerSettings );
				if ( createdId != Guid.Empty )
				{
					if ( keystoreIdToUseForChildren != Guid.Empty )
					{
						string sasToken = await GetDeviceSasTokenAsync( httpClient, keystoreIdToUseForChildren, deviceDescription.hardwareId );
						if ( !string.IsNullOrWhiteSpace( sasToken ) )
						{
							deviceDescription.SasToken = sasToken;
						}
					}
				}
				Console.WriteLine();
			}
		}

		private static async Task<string> GetDeviceSasTokenAsync( HttpClient httpClient, Guid keystoreId, string deviceHardwareId )
		{
			if ( keystoreId == Guid.Empty )
			{
				throw new ArgumentException( $"{nameof( GetDeviceSasTokenAsync )} requires a non empty guid as {nameof( keystoreId )}" );
			}

			if ( string.IsNullOrWhiteSpace( deviceHardwareId ) )
			{
				if ( keystoreId == Guid.Empty )
				{
					throw new ArgumentException( $"{nameof( GetDeviceSasTokenAsync )} requires a value for {nameof( deviceHardwareId )}" );
				}
			}

			var request = HttpMethod.Get.CreateRequest( $"keystores/{keystoreId}/keys/last/token?deviceMac={deviceHardwareId}" );
			var response = await httpClient.SendAsync( request );
			if ( response.IsSuccessStatusCode )
			{
				string sasToken = await response.Content.ReadAsStringAsync();
				Console.WriteLine( $"Retrieved SasToken for device. (hardwareId: {deviceHardwareId}, SaSToken: {sasToken}" );
				return sasToken.Trim( '"' );
			}

			return null;
		}

		private async Task CreateUserRoleAssignmentsAsync( HttpClient httpClient, IList<string> users, Guid spaceId )
		{
			if ( spaceId == Guid.Empty )
			{
				throw new ArgumentException( $"User Role Assignment must have a {nameof( spaceId )}" );
			}

			foreach ( string user in users )
			{
				string oid;
				switch ( user.ToLower() )
				{
					case "employee":
						oid = EmployeeObjectId;
						break;
					case "manager":
						oid = ManagerObjectId;
						break;
					default:
						throw new InvalidDataException( $"User value is not supported: {user}" );
				}

				string spacePath = await GetSpaceFullPathAsync( httpClient, spaceId );

				if ( string.IsNullOrWhiteSpace( spacePath ) )
				{
					Console.WriteLine( $"Unable to get the full path for the current space. Cannot create a role assignment. (Space Id: {spaceId})" );
					continue;
				}

				var roleAssignment = new RoleAssignment
				{
					RoleId = "b1ffdb77-c635-4e7e-ad25-948237d85b30",
					ObjectId = oid,
					TenantId = Tenant,
					Path = spacePath
				};

				Console.WriteLine( $"Creating RoleAssignment: {JsonConvert.SerializeObject( roleAssignment, Formatting.Indented, JsonSerializerSettings )}" );
				var request = HttpMethod.Post.CreateRequest( "roleassignments", JsonConvert.SerializeObject( roleAssignment, JsonSerializerSettings ) );
				await httpClient.SendAsync( request );
			}
		}

		private static async Task<string> GetSpaceFullPathAsync( HttpClient httpClient, Guid spaceId )
		{
			if ( spaceId == Guid.Empty )
			{
				throw new ArgumentException( $"{nameof( GetSpaceFullPathAsync )} must have a {nameof( spaceId )}" );
			}

			var request = HttpMethod.Get.CreateRequest( $"spaces/{spaceId}?includes=fullpath" );
			var response = await httpClient.SendAsync( request );
			if ( response.IsSuccessStatusCode )
			{
				var content = await response.Content.ReadAsStringAsync();
				var resource = JsonConvert.DeserializeObject<Space>( content );
				return resource.SpacePaths.FirstOrDefault();
			}

			return null;
		}
	}
}
