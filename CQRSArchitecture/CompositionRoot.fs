module rec CQRSArchitecture.CompositionRoot

open System
open Serilog
open Newtonsoft.Json
open CQRSArchitecture.Dependencies
open CQRSArchitecture.EventSourcing

open CQRSArchitecture.Listeners.StreamListener
open CQRSArchitecture.Listeners.MessageListener

open CQRSArchitecture.Security
open CQRSArchitecture.Mediation
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.Domain.Messages

open CQRSArchitecture.Domain.Aggregates.CatalogAggregate
open CQRSArchitecture.Domain.Processes.ProductApprovalProcess
open Newtonsoft.Json.Serialization

let composeApplication (log: ILogger) : IDisposable list =
    
    let appVersion = 1

    let nameToType = TypeMapping.nameToType
    let typeToName = TypeMapping.typeToName

    let wallClock () = NodaTime.SystemClock.Instance.GetCurrentInstant()
    let systemClock () = NodaTime.SystemClock.Instance.GetCurrentInstant()

    let toBytes (value: string) = System.Text.Encoding.Default.GetBytes(value)
    let fromBytes (value: byte[]) = System.Text.Encoding.Default.GetString(value)
    let settings =
        Newtonsoft.Json.JsonSerializerSettings(
            ContractResolver = DefaultContractResolver(NamingStrategy = CamelCaseNamingStrategy()),
            Formatting = Formatting.Indented)
    let serialize (value: obj) = JsonConvert.SerializeObject(value, settings) |> toBytes
    let deserialize (bytes: byte[]) (toType: Type) = JsonConvert.DeserializeObject(bytes |> fromBytes, toType, settings)

    let eventStore = getEventStoreClient ()
    let repository = Repository(eventStore, serialize, deserialize, TypeMapping.nameToType, TypeMapping.typeToName, wallClock, systemClock, appVersion)
    let eventWriter = repository :> ICommandRepository

    let redis = getRedisClient()
    let positionPersistence = redisPersistence redis

    let makeName name = { Namespace = "MyApp"; Name = name }
    let startStreamListener = startStreamListener log nameToType deserialize eventStore.CatchUp (Background positionPersistence)   

    let metadata =
        {
            ConversationId = Guid.NewGuid()
            CausedById = None
            OccursAt = systemClock ()
            Principal =
                {
                    Identity = NamedIdentity SystemIdentity
                    AccountId = None
                }
            Version = 1
        }
    let causedById = None
    let commandStreamId = "Commands"
    let expectedVersion = AnyVersion
    
    let commands =
        let sampleCatalogId = Guid.NewGuid()
        let sampleProductId = Guid.NewGuid()
        [
            {
                NewCommand.Id = Guid.NewGuid()
                ExpectedVersion = AnyVersion
                Command =
                    {
                         CatalogId = sampleCatalogId
                    } : CreateCatalogForSellerCommand
            }
            {
                NewCommand.Id = Guid.NewGuid()
                ExpectedVersion = AnyVersion
                Command =
                    {
                         CatalogId = sampleCatalogId
                         ProductId = sampleProductId
                         ProductName = "Xbox"
                    } : AddProductCommand
            }
        ]
    eventWriter.WriteCommand commandStreamId expectedVersion causedById metadata commands |> Async.Ignore |> Async.RunSynchronously

    let startStreamSubscriptions = startStreamListener
    [
        startStreamListener (makeName "Commands") "Commands" 
            <| commandHandlers [
                toHandlerAsync <| createCatalogForSellerCommandHandler repository
                toHandlerAsync <| addProductCommandHandler repository
                toHandlerAsync <| approveProductCommandHandler repository
            ]
            
        startStreamListener (makeName "Events") "Events"
            <| eventHandlers [
                toHandlerAsync <| approvalRequestedEventHandler repository
                toHandlerAsync <| ProductApprovalRequestApprovedEventHandler repository
            ]
    ]


