namespace Haumohio.EventSourcing.Sample
open System
open Microsoft.AspNetCore.Http
open Microsoft.Azure.Functions.Worker
open Haumohio.Azure.Jwt
open Haumohio.Azure


type Query(auth: IAuthenticatedFunctionAccessor) =
  let creds = auth.Context.Value
  member this.me =
    {| name=creds.UserName; client=creds.ClientName; id=creds.ClientId |}

  member this.people () = 
    try
      creds.ClientId
      |> Domain.people
    with
    | exc -> exc.ToString() |> Haumohio.Graphql.dataError 


type Mutations(auth: IAuthenticatedFunctionAccessor)  =
  let creds = auth.Context.Value

  member this.addPerson (personalName:string) (familyName:string) =
    try
      Domain.addPerson creds.ClientId creds.UserName personalName familyName
    with
    | exc -> exc.ToString() |> Haumohio.Graphql.dataError 

  member this.assignRole (personId:string) (roleName:string) =
    Domain.assignRole creds.ClientId creds.UserName personId roleName
