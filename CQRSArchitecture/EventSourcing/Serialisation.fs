module CQRSArchitecture.EventSourcing.Serialisation

open System
open NodaTime
open FSharp.Reflection
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.EventSourcing.PersistenceModels
open CQRSArchitecture.EventSourcing.EventStore

type ResolveMessageType = string -> Type
type Deserialize = byte[] -> Type -> obj

let private (|Guid|_|) (str: string) =
    match System.Guid.TryParse(str) with
    | (true, guid) -> Some(guid)
    | _ -> None

let getType t = t.GetType()
let makeType (t: Type) typeArgs = t.MakeGenericType(typeArgs)

let makeEvent
        (eventId: Guid)
        (metadata: MessageMetadata)
        (position: int64)
        (event: obj) =
    let eventTypeDef = typedefof<Event<_>>
    let typeParams = [| (getType event) |]
    let arguments : obj array = [| eventId ; metadata; position; event |]
    FSharpValue.MakeRecord ((makeType eventTypeDef typeParams), arguments) :?> IEvent

let makeCommand
        (commandId: Guid)
        (metadata: MessageMetadata)
        (expectedVersion: ExpectedVersion)
        (position: int64)
        (command: obj) =
    let commandTypeDef = typedefof<Command<_>>
    let typeParams = [| (getType command) |]
    let arguments : obj array = [| commandId; metadata; expectedVersion; position; command |]
    FSharpValue.MakeRecord ((makeType commandTypeDef typeParams), arguments) :?> ICommand

let makeFault
        (commandId: Guid)
        (metadata: MessageMetadata)
        (position: int64)
        (fault: obj) =
    let faultTypeDef = typedefof<Fault<_>>
    let typeParams = [| (getType fault) |]
    let arguments : obj array = [| commandId; metadata; position; fault |]
    FSharpValue.MakeRecord ((makeType faultTypeDef typeParams), arguments) :?> IFault

let deserializeMessageInternal
        (resolveMessageType: ResolveMessageType)
        (deserialize: Deserialize)
        (messageType: string)
        (messageData: byte[]) =
    [resolveMessageType messageType |> deserialize messageData]

let deserializeMessage
        (deserialize: byte[] -> Type -> obj)
        (nameToType: string -> Type)
        (messageData: ReadMessageData) =
    let storedMetadata = deserialize messageData.Metadata typeof<StoredMetadata> :?> StoredMetadata
    let metadata =
        {
            ConversationId = storedMetadata.ConversationId
            CausedById = storedMetadata.CausedById
            OccursAt = storedMetadata.OccursAt
            Principal = storedMetadata.Principal
            Version = storedMetadata.Version
        }
    let messageType = nameToType messageData.Type
    let payload = deserialize messageData.Data messageType
    match storedMetadata.MessageType with
    | Fault ->
        FaultContainer <| makeFault storedMetadata.MessageId metadata messageData.Position payload
    | Command ->
        let expectedVersion = storedMetadata.ExpectedVersion |> Option.defaultValue NoStream
        CommandContainer <| makeCommand storedMetadata.MessageId metadata expectedVersion messageData.Position payload
    | Event ->
        EventContainer <| makeEvent storedMetadata.MessageId metadata messageData.Position payload

let createMessageData
        (serialize: obj -> byte[])
        (wallClock: unit -> Instant)
        (systemClock: unit -> Instant)
        (typeToTypeName: Type -> string)
        (causedById: Guid option)
        (messageType: MessageType)
        (messageSource: MessageSource)
        (metadata: MessageMetadata)
        (version: int)
        (messageId: Guid)
        (payload: obj)
        (expectedVersion: ExpectedVersion option)
        : MessageData =
    let messageTypeName = typeToTypeName <| payload.GetType()
    let storedMetadata =
        {
            MessageId = messageId
            CausedById = causedById
            ConversationId = metadata.ConversationId
            MessageType = messageType
            WrittenAt = wallClock ()
            OccursAt = systemClock ()
            Principal = metadata.Principal
            Source = messageSource
            Version = version
            ExpectedVersion = expectedVersion
        }

    let serialisedMetadata = serialize storedMetadata
    let serialisedEvent = serialize payload
    {
        Id = messageId
        Type = messageTypeName
        Data = serialisedEvent
        Metadata = serialisedMetadata
    }
