namespace CQRSArchitecture.SerilogEnrichers

open Serilog
open Serilog.Core
open Serilog.Events

type PropertiesLogEnricher (properties: (string * obj) list) =
    interface ILogEventEnricher with
        member this.Enrich(event: LogEvent, propertyFactory: ILogEventPropertyFactory) =
            properties |> List.iter (fun (name, value) -> 
                event.AddPropertyIfAbsent(propertyFactory.CreateProperty(name, value))
            )

[<AutoOpen>]
module SerilogFunctions =

    let logDebug message values (log: ILogger) =
        log.Debug(message, values |> List.toArray)

    let logInfo message values (log: ILogger) =
        log.Information(message, values |> List.toArray)
    
    let logWarning message values (log: ILogger) =
        log.Warning(message, values |> List.toArray)

    let logError message values (log: ILogger) =
        log.Error(message, values |> List.toArray)

    let logErrorEx ex message values (log: ILogger) =
        log.Error(ex, message, values |> List.toArray)

    let logProperties properties (log: ILogger) =
        log.ForContext(PropertiesLogEnricher(properties))
