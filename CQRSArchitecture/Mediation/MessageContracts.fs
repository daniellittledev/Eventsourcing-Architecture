namespace CQRSArchitecture.Mediation.MessageContracts

type IQuery<'Query, 'Result> when 'Query :> IQuery<'Query, 'Result> =
    interface end

type IEvent =
    interface end

type ICommand =
    interface end

type IContext<'Context> =
    abstract member Context: 'Context with get

type IContextPayload<'Payload> =
    abstract member Payload: 'Payload with get

type Context<'Context, 'Payload> =
    {
        Context: 'Context
        Payload: 'Payload
    }
    interface IContext<'Context> with
        member this.Context with get () = this.Context
    interface IContextPayload<'Payload> with
        member this.Payload with get () = this.Payload

[<AutoOpen>]
module MessageContractHelpers = 
    open Microsoft.FSharp.Reflection

    let private contextTypeDef = typedefof<Context<_, _>>
    let makeContextRecord (metadata: 'Metadata) (data: 'Data) =
        let typeParameters = [| typeof<'Metadata>; data.GetType() |]
        let arguments: obj array = [| metadata; data |]
        let recordType = contextTypeDef.MakeGenericType(typeParameters);
        FSharpValue.MakeRecord(recordType, arguments)
