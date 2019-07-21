module CQRSArchitecture.Listeners.MessageListener

open System
open Serilog
open CQRSArchitecture
open CQRSArchitecture.Mediation
open CQRSArchitecture.Mediation.MessageContracts
open CQRSArchitecture.EventSourcing.Messaging

let commandHandlers (commandHandlers: CommandHandler list) (log: ILogger) (result: Result<MessageContainer, Exception>) =
    let services = { Log = log } : Services
    match result with
    | Ok message ->
        match message with
        | CommandContainer c ->
            let m = makeContextRecord services c
            Mediator.send commandHandlers m |> Async.RunSynchronously |> ignore
        | _ -> ()
    | _ -> ()

let eventHandlers (eventHandlers: EventHandler list) (log: ILogger) (result: Result<MessageContainer, Exception>) =
    let services = { Log = log } : Services
    match result with
    | Ok message ->
        match message with
        | EventContainer e ->
            let m = makeContextRecord services e
            Mediator.publish eventHandlers m |> Async.RunSynchronously |> ignore
        | _ -> ()
    | _ -> ()