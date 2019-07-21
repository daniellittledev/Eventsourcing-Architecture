module CQRSArchitecture.Listeners.ConversationListener

open System
open System.Reactive.Linq
open FSharp.Control
open FSharp.Control.Reactive
open Serilog
open System.Threading
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.Listeners.StreamListener

let subscribeToConversation log nameToType deserialize subscribe streamId (timeout: TimeSpan) =
    let cancellationTokenSource = new CancellationTokenSource()
    let token = cancellationTokenSource.Token

    Observable.Create(fun (o : IObserver<_>) ->
        let handleMessage _ (result: Result<MessageContainer, Exception>) =
            match result with
            | Result.Ok message -> o.OnNext(message)
            | Result.Error ex -> o.OnError(ex)
            |> ignore

        let listenerName = { Namespace = "waiter"; Name = "correlation" }
        let mode = (Foreground <| StartingPosition None)
        let disposable = startStreamListener log nameToType deserialize subscribe mode listenerName streamId handleMessage
                
        cancellationTokenSource.CancelAfter(timeout)
        token.Register(fun () ->
            if token.IsCancellationRequested then log.Warning("Conversation subscription was canceled")
            disposable.Dispose()
            o.OnCompleted()
        ) |> ignore
        disposable
    )


let eventsOnly stream =
    stream |> Observable.choose (function
        | EventContainer e -> Some e
        | CommandContainer _ -> None
        | FaultContainer f -> None
    )

let commandsOnly stream =
    stream |> Observable.choose (function
        | EventContainer _ -> None
        | CommandContainer c -> Some c
        | FaultContainer f -> None
    )