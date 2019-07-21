module CQRSArchitecture.Listeners.StreamListener

open System
open EventStore.ClientAPI
open Serilog
open CQRSArchitecture.SerilogEnrichers
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.EventSourcing.EventStore
open CQRSArchitecture.EventSourcing.Serialisation

type StartingPosition =
    | StartingPosition of int64 option

type PersistedPosition =
    {
        SavePosition: string -> int64 -> unit
        LoadPosition: string -> int64 option
    }

type ListenerType =
    | Foreground of StartingPosition
    | Background of PersistedPosition

let private handleException 
    (log: ILogger)
    (ex: Exception)
    (streamId: string)
    (messageData: ReadMessageData)=
    logErrorEx ex "Subscription to {StreamId} faulted while handling message {MessageType} {StreamId}#{MessagePosition}" [streamId; messageData.Type; streamId; messageData.Position] log
    
    //do! eventStore.WriteEvents faultStreamId ExpectedVersion.Any events
    ()

let private onMessageReceived
    (log: ILogger)
    (savePosition: int64 -> unit)
    (nameToType: string -> Type)
    (deserialize: byte[] -> Type -> obj)
    (listenerType: ListenerType)
    (streamId: string)
    (handleMessage: ILogger -> Result<MessageContainer, Exception> -> unit)
    (s: EventStoreCatchUpSubscription)
    (messageData: ReadMessageData) =
    let log =
        logProperties [
            ("MessageId", messageData.Id :> obj)
        ] log
    logInfo "Subscription to {StreamId} received {MessageType} {StreamId}#{MessagePosition}" [streamId; messageData.Type; streamId; messageData.Position] log
    try
        let message = deserializeMessage deserialize nameToType messageData
        handleMessage log (Ok message)
    with
    | ex ->
        match listenerType with
        | Foreground _ ->
            handleMessage log (Error ex)
        | Background _ ->
            // Get that correlation Id!
            handleException log ex streamId messageData

    savePosition messageData.Position

let private onLive (log: ILogger) (s: EventStoreCatchUpSubscription) =
    logInfo "Subscription to {StreamId} switching to live" [s.StreamId] log

let private onSubscriptionDropped (log: ILogger) (s: EventStoreCatchUpSubscription) (r: SubscriptionDropReason) (ex: Exception) =
    logErrorEx ex "Subscription to {StreamId} dropped because of reason {Reason}" [s.StreamId; r] log

type DisposableEventStoreCatchUpSubscription(catchUpSubscription: EventStoreCatchUpSubscription) =
    interface IDisposable with
        member this.Dispose() = catchUpSubscription.Stop()

type ListenerName = {
    Namespace: string
    Name: string
}

type StartCatchUpSubscription = string -> Nullable<int64> -> (EventStoreCatchUpSubscription -> ReadMessageData -> unit) -> (EventStoreCatchUpSubscription -> unit) -> (EventStoreCatchUpSubscription -> SubscriptionDropReason -> exn -> unit) -> EventStoreStreamCatchUpSubscription

let startStreamListener
    (log: ILogger)
    (nameToType: string -> Type)
    (deserialize: byte[] -> Type -> obj)
    (subscribe: StartCatchUpSubscription)
    (listenerType: ListenerType)
    (listenerName: ListenerName)
    (streamId: string)
    (handleMessage: ILogger -> Result<MessageContainer, Exception> -> unit) =

    let streamListenerName = listenerName.Namespace + ":" + listenerName.Name
    let log =
        logProperties [
            ("Source", "StreamListener" :> obj)
            ("StreamListenerName", streamListenerName :> obj)
            ("StreamId", streamId :> obj)
        ] log

    logInfo "Stream listener {StreamListenerName} started" [streamListenerName] log

    let startPostion =
        match listenerType with
        | Foreground (StartingPosition startPostion) -> startPostion
        | Background s ->
            try
                log.Information("Loading restore position")
                s.LoadPosition streamListenerName
            with ex ->
                log.Error(ex, "Could not load restore position for stream listener")
                reraise ()
        |> function
        | Some(value) -> Nullable<int64>(value)
        | None -> Nullable<int64>()

    let savePosition =
        match listenerType with
        | Foreground _ -> ignore
        | Background s -> s.SavePosition streamListenerName

    let subscription =
        subscribe streamId startPostion
            (onMessageReceived log savePosition nameToType deserialize listenerType streamId handleMessage)
            (onLive log)
            (onSubscriptionDropped log)

    new DisposableEventStoreCatchUpSubscription(subscription) :> IDisposable
