[<AutoOpen>]
module CQRSArchitecture.Async

open System
open System.Threading.Tasks

let raiseAsync (ex : #exn) =
    Async.FromContinuations(fun (_,onError,_) -> onError ex)

let private flattenExceptions (ex : AggregateException) =
    ex.Flatten().InnerExceptions |> Seq.item 0

let rewrapAsyncException (asyncWork : Async<'T>) =
    async {
        try
            return! asyncWork
        with
        :? AggregateException as ae ->
            return! (flattenExceptions ae |> raiseAsync)
    }

let taskToAsync (task : Task) =
    task |> Async.AwaitTask |> rewrapAsyncException

let taskWithResultToAsync (task : Task<'T>) =
    task |> Async.AwaitTask |> rewrapAsyncException

// Hide the existing Async functions with these new versions

type Microsoft.FSharp.Control.Async with
    static member AwaitTask t = taskToAsync t

type Microsoft.FSharp.Control.Async with
    static member AwaitTask t = taskWithResultToAsync t