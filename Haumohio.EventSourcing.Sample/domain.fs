namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
  open Haumohio.EventSourcing.EventStorage
  open Haumohio.Storage.Memory

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

  type Person = {
    name: string;
  }

  type DomainEvent =
    | PersonAdded of {| name: string |}

  [<CLIMutable>]
  type PeopleState = {
    people: Person seq
  } with 
    static member Empty = {people=[]}
  let private empty : Person seq = []

  let private container clientId = MemoryStore.container clientId

  let projector (state: Person seq) event =
      match event.details with 
      | PersonAdded x -> state |> Seq.append [{Person.name = x.name}]

  let people clientId  =
    let loader = clientId |> container |> loadState
    loader empty projector

  let addPerson clientId userName (name:string) =
    let c = clientId |> container
    PersonAdded {|name=name|}
    |> storeEvent c userName 
    |> fun x ->
      {|
        at=x.at
        by=x.by
        details=x.details |> DUName
      |}
    