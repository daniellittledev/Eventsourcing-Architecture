namespace CQRSArchitecture

open Serilog
open NodaTime

type Services =
    {
        Log: ILogger
        //Clock: unit -> Instant
    }

