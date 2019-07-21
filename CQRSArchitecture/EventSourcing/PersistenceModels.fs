namespace CQRSArchitecture.EventSourcing.PersistenceModels

open System
open NodaTime
open CQRSArchitecture.Security
open CQRSArchitecture.EventSourcing

type MessageType =
    | Command
    | Event
    | Fault

type MessageSource =
    | Aggregate
    | Process
    | System
    | User

type StoredMetadata =
    {
        MessageId: Guid
        CausedById: Guid option
        ConversationId: Guid
        MessageType: MessageType
        WrittenAt: Instant
        OccursAt: Instant
        Principal: SecurityPrincipal
        Source: MessageSource
        Version: int

        ExpectedVersion: ExpectedVersion option
    }
