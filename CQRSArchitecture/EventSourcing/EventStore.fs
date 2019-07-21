namespace CQRSArchitecture.EventSourcing.EventStore

open System
open FSharp.Control
open EventStore.ClientAPI
open CQRSArchitecture
open CQRSArchitecture.EventSourcing

type MessageData =
    {
        Id: Guid
        Type: string
        Metadata: byte []
        Data: byte []
    }

type ReadMessageData =
    {
        Id: Guid
        Type: string
        Metadata: byte []
        Data: byte []
        Position: Int64
    }

type EventStoreClient(eventstore: IEventStoreConnection) =

    let readBatchSize = 100

    let mapToEventStoreData (e: MessageData) =
        EventData(e.Id, e.Type, true, e.Data, e.Metadata)

    let mapExpectedVersiontoEventStore (expectedVersion: CQRSArchitecture.EventSourcing.ExpectedVersion) =
        match expectedVersion with
        | SpecificVersion x -> x
        | NoStream -> int64 EventStore.ClientAPI.ExpectedVersion.NoStream
        | AnyVersion -> int64 EventStore.ClientAPI.ExpectedVersion.Any

    let mapResolvedEventToEventData (resolvedEvent: ResolvedEvent) =
        let sourceEvent = resolvedEvent.Event
        let eventType = sourceEvent.EventType
        {
            Type = eventType
            Id = sourceEvent.EventId
            Metadata = resolvedEvent.Event.Metadata
            Data = sourceEvent.Data
            Position = resolvedEvent.OriginalEventNumber
        }

    member this.ReadEvents (streamId: string) (start: int) resolveLinks : AsyncSeq<ReadMessageData> =
        let rec readBatch streamId start count = asyncSeq {
            let! batch = eventstore.ReadStreamEventsForwardAsync(streamId, start, count, resolveLinks) |> taskWithResultToAsync

            for resolvedEvent in batch.Events do
                if not (isNull resolvedEvent.Event) then
                    yield (mapResolvedEventToEventData resolvedEvent)

            if not batch.IsEndOfStream then
                yield! readBatch streamId (start + (int64 count)) count
        }
        readBatch streamId (int64 start) readBatchSize

    member this.ReadEventsBackwards (streamId: string) (count: int) resolveLinks : AsyncSeq<ReadMessageData> =
        let rec readBatch streamId start readCount = asyncSeq {
            let totalEventToRead = (count - readCount)
            let batchCount = Math.Min(totalEventToRead, readBatchSize)
            let! batch = eventstore.ReadStreamEventsBackwardAsync(streamId, int64 StreamPosition.End, batchCount, resolveLinks) |> taskWithResultToAsync

            for resolvedEvent in batch.Events do
                if not (isNull resolvedEvent.Event) then
                    yield (mapResolvedEventToEventData resolvedEvent)

            let eventToRead = totalEventToRead - batchCount
            if not batch.IsEndOfStream && eventToRead > 0 then
                yield! readBatch streamId (start + batchCount) (readCount + batchCount)
        }
        readBatch streamId StreamPosition.End 0

    member this.WriteEvents
        (streamId: string)
        (expectedVersion: ExpectedVersion)
        (eventData: MessageData list) = async {
            
        let expectedVersion = mapExpectedVersiontoEventStore expectedVersion
        let clientEventData = eventData |> Seq.map mapToEventStoreData
        let! result = eventstore.AppendToStreamAsync(streamId, int64 expectedVersion, clientEventData) |> taskWithResultToAsync
        return result.NextExpectedVersion
    }

    member this.CatchUp
        (streamId: string)
        (startingPosition: Nullable<int64>)
        (onEvent: EventStoreCatchUpSubscription -> ReadMessageData -> unit)
        (liveProcessingStared: EventStoreCatchUpSubscription -> unit)
        (subscriptionDropped: EventStoreCatchUpSubscription -> SubscriptionDropReason -> exn -> unit) =

        let catchUpSettings = CatchUpSubscriptionSettings(100, 10, false, true)

        let handleEvent subscription (resolvedEvent: ResolvedEvent) =
            if not (isNull resolvedEvent.Event) then
                let eventData = mapResolvedEventToEventData resolvedEvent
                onEvent subscription eventData

        eventstore.SubscribeToStreamFrom(
            streamId,
            startingPosition,
            catchUpSettings,
            handleEvent,
            liveProcessingStared,
            subscriptionDropped
        )
