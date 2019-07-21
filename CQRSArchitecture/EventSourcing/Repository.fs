namespace CQRSArchitecture.EventSourcing

open System
open FSharp.Control
open Serilog
open NodaTime
open CQRSArchitecture.EventSourcing
open CQRSArchitecture.EventSourcing.EventStore
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.EventSourcing.Serialisation
open CQRSArchitecture.EventSourcing.PersistenceModels

type NewCommand =
    {
        Id: Guid
        ExpectedVersion: ExpectedVersion
        Command: obj
    }

type NewEvent =
    {
        Id: Guid
        Event: obj
    }

type IStreamRepository =
    abstract member ReadStream:
        streamId: string -> AsyncSeq<MessageContainer>
    abstract member ReadStreamBackwards:
        streamId: string -> count: int -> AsyncSeq<MessageContainer>

type IAggregateRepository =
    abstract member Load:
        category: string -> aggregateId: 'Id -> AsyncSeq<IEvent>
    abstract member Save:
        category: string -> aggregateId: 'Id -> expectedVersion: ExpectedVersion -> causedById: Guid -> metadata: MessageMetadata -> events: NewEvent list -> Async<int64>

type ISagaRepository =
    abstract member LoadSaga:
        category: string -> sagaId: 'Id -> AsyncSeq<IEvent>
    abstract member SaveSaga:
        category: string -> sagaId: 'Id -> expectedVersion: ExpectedVersion -> causedById: Guid -> metadata: MessageMetadata -> events: NewEvent list -> commands: NewCommand list -> Async<int64>

type ICommandRepository =
    abstract member WriteCommand:
        streamId: string -> expectedVersion: ExpectedVersion -> causedById: Guid option -> metadata: MessageMetadata -> NewCommand list -> Async<int64>

type Repository
        (
            eventStore: EventStoreClient,
            serialize: obj -> byte[],
            deserialize: byte[] -> Type -> obj,
            nameToType: string -> Type,
            typeToName: Type -> string,
            wallClock: unit -> Instant,
            systemClock: unit -> Instant,
            version: int
        ) =

    let createMessageData = createMessageData serialize wallClock systemClock typeToName
    let deserializeMessage = deserializeMessage deserialize nameToType

    let makeStreamId (category: string) (id: 't) =
        category + "-" + id.ToString()

    interface IStreamRepository with
        member this.ReadStream (streamId: string) =
            eventStore.ReadEvents streamId 0 true
            |> AsyncSeq.map deserializeMessage

        member this.ReadStreamBackwards (streamId: string) (count: int) =
            eventStore.ReadEventsBackwards streamId count true
            |> AsyncSeq.map deserializeMessage

    interface IAggregateRepository with
        member this.Load (category: string) (aggregateId: 't) =
            let streamId = makeStreamId category aggregateId
            eventStore.ReadEvents streamId 0 false
            |> AsyncSeq.map deserializeMessage
            |> AsyncSeq.choose (fun m ->
                match m with
                | CommandContainer _ -> None
                | EventContainer e -> Some e
                | FaultContainer _ -> None
            )

        member this.Save (category: string) (aggregateId: 't) (expectedVersion: ExpectedVersion) (causedById: Guid) (metadata: MessageMetadata) (events: NewEvent list) =
            let streamId = makeStreamId category aggregateId
            let createMessage (newEvent: NewEvent) = createMessageData (Some causedById) MessageType.Event MessageSource.Aggregate metadata version newEvent.Id newEvent.Event None
            let messageData = events |> List.map createMessage
            eventStore.WriteEvents streamId expectedVersion messageData

    interface ISagaRepository with
        member this.LoadSaga (category: string) (sagaId: 't) =
            let streamId = makeStreamId category sagaId
            eventStore.ReadEvents streamId 0 false
            |> AsyncSeq.map deserializeMessage
            |> AsyncSeq.choose (fun m ->
                match m with
                | CommandContainer _ -> None
                | EventContainer e -> Some e
                | FaultContainer _ -> None
            )

        member this.SaveSaga (category: string) (sagaId: 't) (expectedVersion: ExpectedVersion) (causedById: Guid) (metadata: MessageMetadata) (events: NewEvent list) (commands: NewCommand list) =
            let streamId = makeStreamId category sagaId
            let createCommand (newCommand: NewCommand) = createMessageData (Some causedById) MessageType.Command MessageSource.Aggregate metadata version newCommand.Id newCommand.Command None
            let createEvent (newEvent: NewEvent) = createMessageData (Some causedById) MessageType.Event MessageSource.Aggregate metadata version newEvent.Id newEvent.Event None
            let eventData = events |> List.map createEvent
            let commandData = commands |> List.map createCommand
            eventStore.WriteEvents streamId expectedVersion (List.append eventData commandData)

    interface ICommandRepository with
        member this.WriteCommand (streamId: string) (expectedVersion: ExpectedVersion) (causedById: Guid option) (metadata: MessageMetadata)  (commands: NewCommand list) =
            let createCommand (newCommand: NewCommand) = createMessageData causedById MessageType.Command MessageSource.Aggregate metadata version newCommand.Id newCommand.Command (Some newCommand.ExpectedVersion)
            let commandData = commands |> List.map createCommand
            eventStore.WriteEvents streamId expectedVersion commandData

[<AutoOpen>]
module Helpers =

    open System.Collections.Generic
    open CQRSArchitecture.EventSourcing.Modeling

    let private checkIfCausedBy (event: IEvent) (markAsSeen: Guid -> unit) =
        match event.Metadata.CausedById with
        | Some causedById -> markAsSeen causedById
        | None -> ()

    let private idempotency () =
        let idempotencySet = HashSet<Guid>()
        let hasNotSeenId id = not <| idempotencySet.Contains(id)
        let markAsSeen (id: Guid) = if hasNotSeenId id then idempotencySet.Add(id) |> ignore
        hasNotSeenId, markAsSeen

    let private formatId (id: obj) =
        match id with
        | :? Guid as g -> g.ToString("N")
        | x -> x.ToString()

    let execAggregate
        (repository: IAggregateRepository)
        (aggregate: Aggregate<'s, 'c, 'e>)
        (log: ILogger)
        (aggregateId: 'id)
        (command: Command<'c>) : Async<unit> = async {

        log.Information("Aggregate {AggregateType}#{AggregateId} handling message {MessageType}#{MessageId}", 
            aggregate.Name, aggregateId, command.Command.GetType(), command.Id)
        let aggregateId = formatId aggregateId 
        let hasNotSeenId, markAsSeen = idempotency ()
        let history = repository.Load aggregate.Name aggregateId
        let! state =
            history
            |> AsyncSeq.map (fun event ->
                checkIfCausedBy event markAsSeen
                aggregate.MapEventIn event
            )
            |> AsyncSeq.fold aggregate.Apply aggregate.InitialState

        if hasNotSeenId command.Id then
            let events =
                aggregate.Execute state command
                |> List.map (fun e -> { Id = e.Id; Event = aggregate.MapEventOut e.Event } : NewEvent)
            do! repository.Save aggregate.Name aggregateId command.ExpectedVersion command.Id command.Metadata events |> Async.Ignore
            for event in events do log.Information("Event persisted {EventType}", event.Event.GetType())
    }

    let execProcess
        (repository: ISagaRepository)
        (processManager: ProcessManager<'s, 'external, 'e>)
        (log: ILogger)
        (processId: 'id)
        (externalEvent: Event<'external>) : Async<unit> = async { 

        log.Information("Process {ProcessType}#{ProcessId} handling message {MessageType}#{MessageId}", 
            processManager.Name, processId, externalEvent.Event.GetType(), externalEvent.Id)
        let processId = formatId processId
        let mutable expectedVersion = NoStream
        let hasNotSeenId, markAsSeen = idempotency ()
        let history = repository.LoadSaga processManager.Name processId
        let! state =
            history
            |> AsyncSeq.map (fun event ->
                expectedVersion <- SpecificVersion event.Position
                checkIfCausedBy event markAsSeen
                processManager.MapEventIn event
            )
            |> AsyncSeq.fold processManager.Apply processManager.InitialState

        if hasNotSeenId externalEvent.Id then
            let events, commands = processManager.ReactTo state externalEvent

            // Todo: use correct expected version
            let events = events |> List.map (fun e -> { Id = e.Id; Event = processManager.MapEventOut e.Event } : NewEvent)
            let commands = commands |> List.map (fun c -> { Id = c.Id; ExpectedVersion = c.ExpectedVersion; Command = c.Command } : NewCommand)
            do! repository.SaveSaga processManager.Name processId expectedVersion externalEvent.Id externalEvent.Metadata events commands |> Async.Ignore
            for event in events do log.Information("Event persisted {EventType}", event.Event.GetType())
            for command in commands do log.Information("Command persisted {CommandType}", command.Command.GetType())
    }

