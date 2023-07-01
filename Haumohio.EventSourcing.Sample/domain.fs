namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open System.Text.RegularExpressions
  open HashidsNet
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
  open Haumohio.EventSourcing.EventStorage
  open Haumohio.Storage.Memory


  let private hasher salt=
    new Hashids(salt, minHashLength=8, alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ23456789")

  let calcId prefix clientid=
    let result = 
      hasher(clientid).Encode(DateTime.UtcNow.Ticks / 1000L |> int)
      |> fun x -> Regex.Replace(x, ".{4}", "$0-")
      |> fun x -> x.Remove( x.Length - 1)
    prefix + "-" + result 

  type DomainEvent =
    | PersonAdded of {| id: string; personalName:string; familyName: string |}
    | RoleAssigned of {| personId: string; roleName: string |}
    interface IHasDescription with
        member this.description: string = 
          match this with 
          | PersonAdded x -> x.id
          | RoleAssigned x -> x.roleName


  type Person = {
    id: string
    personalName: string;
    familyName: string;
    roles: string Set
  }

  let private empty : Person seq = []

  let private container clientId = MemoryStore.container clientId

  let projector (state: Person seq) event =
      match event.details with 
      | PersonAdded x -> state |> Seq.append [{Person.id = x.id; personalName = x.personalName; familyName = x.familyName; roles = set [] }]
      | RoleAssigned x -> 
          match state |> Seq.tryFind (fun p -> p.id =x.personId) with 
          | None -> state
          | Some person ->
            let updated = {person with roles = person.roles |> Set.add x.roleName }
            state |> Seq.map ( fun p -> if p.id = x.personId then updated else p )

  let people clientId  =
    let loader = clientId |> container |> loadState
    loader empty projector

  let addPerson clientId userName personalName familyName =
    let c = clientId |> container
    let eventDetail = {| id=calcId "P" clientId; personalName=personalName; familyName=familyName |}
    eventDetail
    |> PersonAdded
    |> storeEvent c userName 

  let assignRole clientId userName personId rolename =
    let c = clientId |> container
    let eventDetail = {| personId = personId; roleName = rolename |}
    eventDetail
    |> RoleAssigned
    |> storeEvent c userName 
