namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open System.Text.RegularExpressions
  open HashidsNet
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
  open Haumohio.EventSourcing.EventStorage
  open Haumohio.Storage.Memory

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

  type Person = {
    id: string
    personalName: string;
    familyName: string;
  }

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

  [<CLIMutable>]
  type PeopleState = {
    people: Person seq
  } with 
    static member Empty = {people=[]}

  let private empty : Person seq = []

  let private container clientId = MemoryStore.container clientId

  let projector (state: Person seq) event =
      match event.details with 
      | PersonAdded x -> state |> Seq.append [{Person.id = x.id; personalName = x.personalName; familyName = x.familyName}]

  let people clientId  =
    let loader = clientId |> container |> loadState
    loader empty projector

  let addPerson clientId userName personalName familyName =
    let c = clientId |> container
    let eventDetail = {| id=calcId "P" clientId; personalName=personalName; familyName=familyName |}
    eventDetail
    |> PersonAdded
    |> storeEvent c userName 
    |> fun x ->
      {|
        at=x.at
        by=x.by
        details={| ``PersonAdded`` = eventDetail|}
      |}
    