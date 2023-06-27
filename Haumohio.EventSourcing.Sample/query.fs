namespace Haumohio.EventSourcing.Sample
open System
open Microsoft.AspNetCore.Http
open Microsoft.Azure.Functions.Worker
open Haumohio.Azure.Jwt
open Haumohio.Azure


type Query(fcAccessor: IFunctionContextAccessor) =

  member private this.credentials =
    match fcAccessor.FunctionContext with 
    | Some context -> 
        let name = context.JwtUser.FindFirst("name").Value
        let apiKey, clientName = 
          match context.JwtApiKey with 
          | Some x -> x.FindFirst("clientId").Value, x.FindFirst("client").Value
          | None -> failwith "invalid credentials"
        {| name=name; apiKey=apiKey; clientName=clientName |}
    | None -> failwith "No context provided"

  member this.me =
    {| name=this.credentials.name; client=this.credentials.clientName |}

  member this.people () = 
    Domain.people(this.credentials.apiKey).people


type Mutations(fcAccessor: IFunctionContextAccessor)  =
  member private this.credentials =
    match fcAccessor.FunctionContext with 
    | Some context -> 
        let name = context.JwtUser.FindFirst("name").Value
        let apiKey, clientName = 
          match context.JwtApiKey with 
          | Some x -> x.FindFirst("clientId").Value, x.FindFirst("client").Value
          | None -> failwith "invalid credentials"
        {| name=name; apiKey=apiKey; clientName=clientName |}
    | None -> failwith "No context provided"

  member this.addPerson (name:string) =
    Domain.addPerson this.credentials.apiKey name
    