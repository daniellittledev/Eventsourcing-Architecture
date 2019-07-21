module CQRSArchitecture.Domain.Messages

open System

(*
    Sample Domain: a Product is added to the Catalog of a Marketplace by a Seller, and it has to go through a Quality Control Process;
*)

// Messages

type CreateCatalogForSellerCommand =
    {
        CatalogId: Guid
    }

type CatalogCreatedEvent =
    {
        CatalogId: Guid
    }

type AddProductCommand =
    {
        CatalogId: Guid
        ProductId: Guid
        ProductName: string
    }

type ProductAddedEvent =
    {
        CatalogId: Guid
        ProductId: Guid
        ProductName: string
    }

type RequestProductApprovalCommand =
    {
        CatalogId: Guid
        ProductId: Guid
    }
    
type ProductApprovalRequestApprovedEvent =
    {
        CatalogId: Guid
        ProductId: Guid
    }

type ProductApprovalRequestRejectedEvent =
    {
        CatalogId: Guid
        ProductId: Guid
    }

type ApproveProductCommand =
    {
        CatalogId: Guid
        ProductId: Guid
    }

type ProductApprovedEvent =
    {
        CatalogId: Guid
        ProductId: Guid
    }

// Process

type ApprovalRequestedEvent =
    {
        CatalogId: Guid
        ProductId: Guid
    }

type ApprovalResponseRecievedEvent =
    {
        CatalogId: Guid
        ProductId: Guid
    }
