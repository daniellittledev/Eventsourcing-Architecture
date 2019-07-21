namespace CQRSArchitecture.Mediation

open System
open CQRSArchitecture.Mediation.MessageContracts

type Handler<'R> = Type * (obj -> Async<'R>)
type QueryHandler = Handler<obj>
type EventHandler = Handler<Unit>
type CommandHandler = Handler<Unit>

[<AutoOpen>]
module Mediator =

    let toQueryHandler<'q, 'r when 'q :> IQuery<'q, 'r>> (func: 'q -> 'r) =
        typeof<'q>, (fun (message: obj) -> async {
            return func (message :?> 'q) :> obj
        })

    let toQueryHandlerAsync<'q, 'r when 'q :> IQuery<'q, 'r>> (func: 'q -> Async<'r>) =
        typeof<'q>, (fun (message: obj) -> async {
            let! result = func (message :?> 'q)
            return result :> obj
        })

    let toHandler (func: 'm -> unit) =
        typeof<'m>, (fun (message: obj) -> async {
            return func (message :?> 'm)
        })

    let toHandlerAsync (func: 'm -> Async<unit>) =
        typeof<'m>, (fun (message: obj) -> func (message :?> 'm))

    let rec execute
        (handlers: Handler<'r> list)
        (messageType: Type)
        (message: obj) = async {
        let matchingHandlers = handlers |> List.filter (fun (t, _) -> messageType = t)
        let mutable result = () :> obj
        for (_, handler) in matchingHandlers do
            let! r = handler message
            result <- r
        return result
    }

    let query<'Query, 'Result when 'Query :> IQuery<'Query, 'Result>> (queryHandlers: QueryHandler list) (query: 'Query) : Async<'Result> = async {
        let queryType = query.GetType()
        let! result = execute queryHandlers queryType query
        return result :?> 'Result
    }

    let send (commandHandlers: CommandHandler list) (command) : Async<Unit> =
        let commandType = command.GetType()
        let result = execute commandHandlers commandType command
        result |> Async.Ignore

    let publish (eventHandlers: EventHandler list) (event) : Async<Unit> =
        let eventType = event.GetType()
        let result = execute eventHandlers eventType event
        result |> Async.Ignore
