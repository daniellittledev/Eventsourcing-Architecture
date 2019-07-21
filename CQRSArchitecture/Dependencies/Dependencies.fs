[<AutoOpen>]
module CQRSArchitecture.Dependencies.Functions

open System
open EventStore.ClientAPI
open StackExchange.Redis

open CQRSArchitecture.EventSourcing.EventStore
open CQRSArchitecture.Listeners.StreamListener

open CQRSArchitecture.Domain.Messages

let getEventStoreClient () =
    let connectionSettings = ConnectionSettings.Default
    let connection = EventStoreConnection.Create(connectionSettings, Uri("tcp://admin:changeit@localhost:1113"), "My App Connection")
    connection.ConnectAsync() |> Async.AwaitTask |> Async.RunSynchronously
    let client = EventStoreClient(connection)
    client

let getRedisClient () =
    let redis = ConnectionMultiplexer.Connect("localhost")
    redis

module TypeMapping = 
    let serializedTypes =
        [
            typeof<CreateCatalogForSellerCommand>
            typeof<CatalogCreatedEvent>
            typeof<AddProductCommand>
            typeof<ProductAddedEvent>
            typeof<ProductApprovalRequestApprovedEvent>
            typeof<ProductApprovalRequestRejectedEvent>
            typeof<ApproveProductCommand>
            typeof<ProductApprovedEvent>
            typeof<ApprovalRequestedEvent>
            typeof<ApprovalResponseRecievedEvent>
        ]
    let mapNameToType = Map (serializedTypes |> List.map (fun t -> (t.Name, t))) 
    let nameToType (name: string) =
        mapNameToType |> Map.find name
    let typeToName (fromType: Type) =
        fromType.Name

let redisPersistence (redis: ConnectionMultiplexer) =
    let savePosition (key: string) (value: int64) =
        let db = redis.GetDatabase()
        db.StringSet(RedisKey.op_Implicit(key), RedisValue.op_Implicit(value.ToString()))
        |> ignore
    let loadPosition (key: string) =
        let db = redis.GetDatabase()
        let value = db.StringGet(RedisKey.op_Implicit(key))
        if value.HasValue then
            value.ToString() |> Int64.Parse |> Some
        else None        
    {
        SavePosition = savePosition
        LoadPosition = loadPosition
    }


