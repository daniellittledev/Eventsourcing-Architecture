namespace CQRSArchitecture.Security

open System

type PrincipalIdentity =
    | NamedIdentity of NamedIdentity
    | UserIdentity of UserIdentity

and NamedIdentity =
    | SystemIdentity
    | Anonymous

and UserIdentity =
    {
        Id: string;
        Permissions: string list;
    }

type SecurityPrincipal =
    {
        Identity : PrincipalIdentity
        AccountId : Guid option
    }
