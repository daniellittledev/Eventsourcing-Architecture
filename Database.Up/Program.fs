open System

open EventStore.ClientAPI

open Serilog
open Serilog.Exceptions
open EventStore.ClientAPI.Common.Log
open System.Net

let getEventStoreConnection () =
    let connectionSettings = ConnectionSettings.Default
    let connection = EventStoreConnection.Create(connectionSettings, Uri("tcp://admin:changeit@localhost:1113"), "My App Connection")
    connection.ConnectAsync() |> Async.AwaitTask |> Async.RunSynchronously
    connection

let getEventStoreHttpClient () =
    EventStore.ClientAPI.Projections.ProjectionsManager(ConsoleLogger(), IPEndPoint(IPAddress.Parse("127.0.0.1"), 2113), TimeSpan.FromMilliseconds(5000.0))

[<EntryPoint>]
let main argv =
    let appName = "Database Up"
    use log =
        LoggerConfiguration()
            .Enrich.WithProperty("AppName", appName)
            .Enrich.WithExceptionDetails()
            .WriteTo.Console()
            .CreateLogger()
    log.Information("Hello")

    let es = getEventStoreHttpClient()
    let creds = EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit")
    
    let name = "EventsProjection"
    let query = """
    fromAll()
    .when({
        $any: function (state, event) {
            if (event.metadata
                && event.metadata.messageType.case == 'Event'
                && event.metadata.source.case == 'Aggregate') {
                linkTo('Events', event);
            }
        }
    });
    """
    es.CreateContinuousAsync(name, query, creds) |> Async.AwaitTask |> Async.RunSynchronously

    let name = "ConversationProjection"
    let query = """
    fromAll()
    .when({
        $any: function (state, event) {
            if (event.metadata) {
                var messageType = event.metadata.messageType.case;
                if (messageType == 'Event' || messageType == 'Fault') {
                    var guid = event.metadata.conversationId.replace(/-/g, '');
                    linkTo('Conversation-' + guid, event);
                }
            }
        }
    });
    """
    es.CreateContinuousAsync(name, query, creds) |> Async.AwaitTask |> Async.RunSynchronously
    
    log.Information("Goodbye")
    0 // return an integer exit code
