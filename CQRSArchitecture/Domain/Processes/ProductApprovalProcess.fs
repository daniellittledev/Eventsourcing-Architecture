module CQRSArchitecture.Domain.Processes.ProductApprovalProcess

open System
open CQRSArchitecture.EventSourcing
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.EventSourcing.Modeling
open CQRSArchitecture.Domain.Messages

type ProductApprovalExternalEvent =
    | ProductAddedEvent of ProductAddedEvent
    | ProductApprovalRequestApprovedEvent of ProductApprovalRequestApprovedEvent

// Talking point: Process event names
type ProductApprovalEvent =
    | ApprovalRequestedEvent of ApprovalRequestedEvent
    | ApprovalResponseRecievedEvent of ApprovalResponseRecievedEvent

type ProductApprovalState =
    | Uninitialized
    | WaitingForProductApproval
    | ProductApproved

let mapEventIn (event: IEvent) =
    { Id = event.Id; Metadata = event.Metadata; Position = event.Position; Event =
        match event.Event with
        | :? ApprovalRequestedEvent as e -> ApprovalRequestedEvent e
        | :? ApprovalResponseRecievedEvent as e -> ApprovalResponseRecievedEvent e
        | x -> failwithf "Unknown event: %O" x
    }

let mapEventOut = function
    | ApprovalRequestedEvent e -> e :> obj
    | ApprovalResponseRecievedEvent e -> e :> obj

let withDerivedId (previousMessageId: Guid) (e: 'e) : NewEvent<'e> =
    {
        Id = Guid.NewGuid() // TODO
        Event = e
    }

// Talking point: tuple match
let reactTo (state: ProductApprovalState) (external: Event<ProductApprovalExternalEvent>) : (NewEvent<ProductApprovalEvent> list) * (NewCommand<obj> list) =
    match state, external.Event with
    | Uninitialized, ProductAddedEvent e ->
        [ApprovalRequestedEvent { CatalogId = e.CatalogId; ProductId = e.ProductId }],
        []
    | Uninitialized, _ ->
        [(* Approval already requested *)], []
    | WaitingForProductApproval, ProductApprovalRequestApprovedEvent e ->
        [ApprovalResponseRecievedEvent { CatalogId = e.CatalogId; ProductId = e.ProductId }],
        [{ NewCommand.Id = Guid.NewGuid(); ExpectedVersion = AnyVersion; Command = { CatalogId = e.CatalogId; ProductId = e.ProductId } :> obj }]
    | _ ->
        [], []
    |> (fun (events, commands) ->
        events |> List.map (withDerivedId external.Id),
        commands
    )    

// Talking point: no validation here
let apply (state: ProductApprovalState) (event: Event<ProductApprovalEvent>) : ProductApprovalState =
    match event.Event with
    | ApprovalRequestedEvent e -> WaitingForProductApproval
    | ApprovalResponseRecievedEvent e -> ProductApproved

let productApprovelProcess : ProcessManager<ProductApprovalState, ProductApprovalExternalEvent, ProductApprovalEvent> = 
    {
        Name = "ProductApproval"
        InitialState = Uninitialized

        MapEventIn = mapEventIn
        MapEventOut = mapEventOut

        ReactTo = reactTo
        Apply = apply
    }

// Handlers

open CQRSArchitecture
open CQRSArchitecture.EventSourcing
open CQRSArchitecture.Domain.Handlers
open CQRSArchitecture.Mediation.MessageContracts

let approvalRequestedEventHandler repository (m: Context<Services, Event<ProductAddedEvent>>) = async {
    do! execProcess repository productApprovelProcess
        |> applyEvent m (fun x -> x.ProductId) ProductAddedEvent
}

let ProductApprovalRequestApprovedEventHandler repository (m: Context<Services, Event<ProductApprovalRequestApprovedEvent>>) = async {
    do! execProcess repository productApprovelProcess
        |> applyEvent m (fun x -> x.ProductId) ProductApprovalRequestApprovedEvent
}