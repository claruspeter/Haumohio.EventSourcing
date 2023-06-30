namespace Haumohio.EventSourcing.Sample
open System
open Microsoft.AspNetCore.Http
open Microsoft.Azure.Functions.Worker
open Haumohio.Azure.Jwt
open Haumohio.Azure


type Query(auth: IAuthenticatedFunctionAccessor) =
  let creds = auth.Context.Value
  member this.me =
    {| name=creds.UserName; client=creds.ClientName |}

  member this.people () = 
    creds.ClientId
    |> Domain.people


type Mutations(auth: IAuthenticatedFunctionAccessor)  =
  let creds = auth.Context.Value

  member this.addPerson (personalName:string) (familyName:string) =
    Domain.addPerson creds.ClientId creds.UserName personalName familyName
