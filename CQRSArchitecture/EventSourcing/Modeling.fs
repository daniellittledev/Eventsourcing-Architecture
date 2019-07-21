namespace CQRSArchitecture.EventSourcing.Modeling

open System
open CQRSArchitecture.EventSourcing
open CQRSArchitecture.EventSourcing.Messaging

type NewCommand<'t> =
    {
        Id: Guid
        ExpectedVersion: ExpectedVersion
        Command: 't
    }

type NewEvent<'t> =
    {
        Id: Guid
        Event: 't
    }

type Aggregate<'State, 'Command, 'Event> =
    {
        Name: string
        InitialState: 'State

        MapEventIn: IEvent -> Event<'Event>
        MapEventOut: 'Event -> obj

        Execute: 'State -> Command<'Command> -> NewEvent<'Event> list
        Apply: 'State -> Event<'Event> -> 'State
    }

type ProcessManager<'State, 'External, 'Event> =
    {
        Name: string
        InitialState: 'State

        MapEventIn: IEvent -> Event<'Event>
        MapEventOut: 'Event -> obj

        ReactTo: 'State -> Event<'External> -> (NewEvent<'Event> list) * (NewCommand<obj> list)
        Apply: 'State -> Event<'Event> -> 'State
    }

type Projector<'State> =
    {
        StreamName: string
        InitialState: 'State
        Apply: 'State -> IEvent -> 'State
    }

type Reactor<'State, 'Event> =
    {
        StreamName: string
        ReactTo: 'State -> IEvent -> NewEvent<'Event> option
        Apply: 'State -> IEvent -> 'State
    }
