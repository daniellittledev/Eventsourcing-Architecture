module CQRSArchitecture.Domain.Aggregates.CatalogAggregate

open System
open CQRSArchitecture.EventSourcing.Messaging
open CQRSArchitecture.EventSourcing.Modeling
open CQRSArchitecture.Domain.Messages

(*
    Sample Domain: a Product is added to the Catalog of a Marketplace by a Seller, and it has to go through a Quality Control Process;
*)

// Aggregates

type CatalogCommand =
    | CreateCatalogForSellerCommand of CreateCatalogForSellerCommand
    | AddProductCommand of AddProductCommand
    | ApproveProductCommand of ApproveProductCommand

type CatalogEvent =
    | CatalogCreatedEvent of CatalogCreatedEvent
    | ProductAddedEvent of ProductAddedEvent
    | ProductApprovedEvent of ProductApprovedEvent

type ProductId = Guid
    
type ProductStatus =
    | Pending
    | Approved

type CatalogState =
    {
        CatalogId: Guid
        Products: Map<ProductId, ProductStatus>
    }

let withDerivedId (previousMessageId: Guid) (e: 'e) : NewEvent<'e> =
    {
        Id = Guid.NewGuid() // TODO
        Event = e
    }

let execute (state: CatalogState option) (message: Command<CatalogCommand>) : NewEvent<CatalogEvent> list = 
    let metadata = message.Metadata
    let command = message.Command
    match state with
    | Some s ->
        match command with
        | CreateCatalogForSellerCommand create ->
            [(* Catalog has already been created *)]
        | AddProductCommand addProduct ->
            s.Products
            |> Map.tryFind addProduct.ProductId
            |> Option.map (fun _ ->
                [(* Product has already been added *)])
            |> Option.defaultValue
                [ProductAddedEvent {
                    CatalogId = addProduct.CatalogId
                    ProductId = addProduct.ProductId
                    ProductName = addProduct.ProductName
                }]
        | ApproveProductCommand approveProduct ->
            s.Products
            |> Map.tryFind approveProduct.ProductId
            |> Option.map (fun p ->
                match p with
                | Pending ->
                    [ProductApprovedEvent {
                        CatalogId = approveProduct.CatalogId
                        ProductId = approveProduct.ProductId
                    }]
                | Approved -> [(* Product has already been approved *)])
            |> Option.defaultValue
                [(* Product does not exist *)]
    | None ->
        match command with
        | CreateCatalogForSellerCommand create ->
            [CatalogCreatedEvent {
                CatalogId = create.CatalogId
            }]
        | _ -> [(* Not in currect state event *)]
    |> List.map (withDerivedId message.Id)

let apply (state: CatalogState option) (event: Event<CatalogEvent>) : CatalogState option = 
    match state with
    | None ->
        match event.Event with
        | CatalogCreatedEvent e ->
            Some {
                CatalogId = e.CatalogId
                Products = Map []
            }
        | _ -> state
    | Some s ->
        match event.Event with
        | CatalogCreatedEvent e -> state
        | ProductAddedEvent e ->
            Some {
                CatalogId = s.CatalogId
                Products = s.Products |> Map.add e.ProductId Pending
            }
        | ProductApprovedEvent e -> 
            Some {
                CatalogId = s.CatalogId
                Products = s.Products |> Map.add e.ProductId Approved
            }

let mapEventIn (e: IEvent) =
    { Id = e.Id; Metadata = e.Metadata; Position = e.Position; Event =
        match e.Event with
        | :? CatalogCreatedEvent as x -> CatalogCreatedEvent x
        | :? ProductAddedEvent as x -> ProductAddedEvent x
        | :? ProductApprovedEvent as x -> ProductApprovedEvent x
        | x -> failwithf "Unknown event: %O" x
    }

let mapEventOut = function
    | CatalogCreatedEvent x -> x :> obj
    | ProductAddedEvent x -> x :> obj
    | ProductApprovedEvent x -> x :> obj

let catalog : Aggregate<CatalogState option, CatalogCommand, CatalogEvent> = 
    {
        Name = "Catalog"
        InitialState = None

        MapEventIn = mapEventIn
        MapEventOut = mapEventOut

        Execute = execute
        Apply = apply
    }

// Handlers

open CQRSArchitecture
open CQRSArchitecture.EventSourcing
open CQRSArchitecture.Domain.Handlers
open CQRSArchitecture.Mediation.MessageContracts

let createCatalogForSellerCommandHandler repository (m: Context<Services, Command<CreateCatalogForSellerCommand>>) = async {
    do! execAggregate repository catalog
        |> applyCommand m (fun x -> x.CatalogId) CreateCatalogForSellerCommand
}

let addProductCommandHandler repository (m: Context<Services, Command<AddProductCommand>>) = async {
    do! execAggregate repository catalog
        |> applyCommand m (fun x -> x.CatalogId) AddProductCommand
}

let approveProductCommandHandler repository (m: Context<Services, Command<ApproveProductCommand>>) = async {
    do! execAggregate repository catalog
        |> applyCommand m (fun x -> x.CatalogId) ApproveProductCommand
}