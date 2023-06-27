namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
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
    let event = { at = DateTime.UtcNow; by = userName; details = (PersonAdded {|name=name|}) }
    printfn "%s"  $"event_{event.at: u}"
    c.save 
      $"event_{event.at: u}"
      event
      |> ignore
    {|
      at=event.at
      by=event.by
      details=event.details |> DUName
    |}
    