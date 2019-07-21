namespace CQRSArchitecture.EventSourcing.Messaging

open System
open NodaTime
open CQRSArchitecture.Security
open CQRSArchitecture.EventSourcing

type MessageMetadata =
    {
        ConversationId: Guid
        CausedById: Guid option
        OccursAt: Instant
        Principal: SecurityPrincipal
        Version: int
    }

type Command<'Command> =
    {
        Id: Guid
        Metadata: MessageMetadata
        ExpectedVersion: ExpectedVersion
        Position: int64
        Command: 'Command
    }
    interface ICommand with
        member this.Id = this.Id
        member this.Metadata = this.Metadata
        member this.ExpectedVersion = this.ExpectedVersion
        member this.Position = this.Position
        member this.Command = this.Command :> obj

and ICommand =
    abstract member Id: Guid
    abstract member Metadata: MessageMetadata
    abstract member ExpectedVersion: ExpectedVersion
    abstract member Position: int64
    abstract member Command: obj

type Event<'Event> =
    {
        Id: Guid
        Metadata: MessageMetadata
        Position: int64
        Event: 'Event
    }
    interface IEvent with
        member this.Id = this.Id
        member this.Metadata = this.Metadata
        member this.Position = this.Position
        member this.Event = this.Event :> obj

and IEvent =
    abstract member Id: Guid
    abstract member Metadata: MessageMetadata
    abstract member Position: int64
    abstract member Event: obj

type Fault<'t> =
    {
        Id: Guid
        Metadata: MessageMetadata
        Position: int64
        Fault: FaultDetails
    }
    interface IFault with
        member this.Id = this.Id
        member this.Metadata = this.Metadata
        member this.Position = this.Position
        member this.Fault = this.Fault :> obj

and IFault =
    abstract member Id: Guid
    abstract member Metadata: MessageMetadata
    abstract member Position: int64
    abstract member Fault: obj

and FaultDetails =
    {
        StreamName: string
        EventNumber: int
        MessageType: string
        MessageException: Exception
    }

type MessageContainer =
    | CommandContainer of ICommand
    | EventContainer of IEvent
    | FaultContainer of IFault