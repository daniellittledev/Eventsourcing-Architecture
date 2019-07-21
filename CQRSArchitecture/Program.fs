open System
open Serilog
open Serilog.Exceptions
open CQRSArchitecture
open CQRSArchitecture.Infrastructure.ServiceHosting

[<EntryPoint>]
let main argv =
    let serviceName = "Sample App"
    use log =
        LoggerConfiguration()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithExceptionDetails()
            .WriteTo.Console()
            .CreateLogger()
    startService log serviceName (fun () ->
        let start, stop =
            try
                log.Information("Initialising Service {ServiceName}", serviceName)

                let mutable listeners: IDisposable list = []
                let start () =
                    let log = log.ForContext("ServiceName", serviceName).ForContext("TraceId", "Global")
                    listeners <- CompositionRoot.composeApplication log

                let stop () =
                    for listener in listeners do
                        listener.Dispose()

                (start, stop)
            with ex ->
                log.Fatal(ex, "Failed to initialise Service");
                reraise ()

        (fun () -> start()),
        (fun () -> stop())
    )
    0 // return an integer exit code
