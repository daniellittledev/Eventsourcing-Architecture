module CQRSArchitecture.Domain.Handlers

open CQRSArchitecture
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.Mediation.MessageContracts

let applyCommand (m: Context<Services, Command<'m>>) (getId: 'm -> 'id) (mapPayload: 'm -> 'u) f =
    let copyWithNewPayload (m: Command<'m>) (payload: 'u) =
        {
            Id = m.Id
            Metadata = m.Metadata
            ExpectedVersion = m.ExpectedVersion
            Position = m.Position
            Command = payload
        }
    f m.Context.Log (getId m.Payload.Command) (copyWithNewPayload m.Payload (mapPayload m.Payload.Command))


let applyEvent (m: Context<Services, Event<'m>>) (getId: 'm -> 'id) (mapPayload: 'm -> 'u) f =
    let copyWithNewPayload (m: Event<'m>) (payload: 'u) =
        {
            Id = m.Id
            Metadata = m.Metadata
            Position = m.Position
            Event = payload
        }
    f m.Context.Log (getId m.Payload.Event) (copyWithNewPayload m.Payload (mapPayload m.Payload.Event))
