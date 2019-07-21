module CQRSArchitecture.Infrastructure.ServiceHosting

open System
open Serilog
open Topshelf
open Topshelf.ServiceConfigurators
open Topshelf.HostConfigurators
open Topshelf.Runtime

type ServiceType = (unit -> unit) * (unit -> unit)

let startService (log: ILogger) (serviceName: string) (create: unit -> ServiceType) =
    log.Information("Hello")
    HostFactory.Run(fun (configurator: HostConfigurator) ->
        configurator.UseSerilog()
        configurator.Service<ServiceType>(fun (settings: ServiceConfigurator<ServiceType>) ->
                settings.ConstructUsing(fun (hostSettings: HostSettings) -> create ())
                settings.WhenStarted(fun (start, _) -> start ()) |> ignore
                settings.WhenStopped(fun (_, stop) -> stop ()) |> ignore
                settings.WhenShutdown(fun (_, stop) -> stop ()) |> ignore
            ) |> ignore
        configurator.RunAsLocalSystem() |> ignore
        configurator.SetServiceName(serviceName)
        configurator.SetDisplayName(serviceName)
        configurator.SetDescription(serviceName)
        configurator.OnException(fun ex -> log.Fatal(ex, "Topshelf reported exception"));
        configurator.EnableServiceRecovery(fun recoveryConfigurator ->
            recoveryConfigurator.RestartService(delayInMinutes = 3) |> ignore
            recoveryConfigurator.SetResetPeriod(days = 1) |> ignore
        ) |> ignore
    ) |> ignore
    log.Information("Goodbye")