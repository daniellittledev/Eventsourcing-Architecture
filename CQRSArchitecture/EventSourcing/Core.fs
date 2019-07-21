namespace CQRSArchitecture.EventSourcing

type ExpectedVersion =
    | NoStream
    | SpecificVersion of int64
    | AnyVersion
