namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
  open Haumohio.Storage.Memory


  type Person = {
    name: string;
  }

  type DomainEvent =
    | PersonAdded of {| name: string |}

  type DomainEvent1 = Event<DomainEvent>

  [<CLIMutable>]
  type PeopleState = {
    people: Person seq
  } with 
    static member Empty = {people=[]}

  let private container apiKey = MemoryStore.container apiKey

  let projector state event =
      match event.details with 
      | PersonAdded x -> {state with people= state.people |> Seq.append [{Person.name = x.name}]}

  let people apiKey  =
    let loader = apiKey |> container |> loadState
    loader PeopleState.Empty projector

  let addPerson apiKey (name:string) =
    let c = apiKey |> container
    { at = DateTime.UtcNow; by = "me"; details = (PersonAdded {|name=name|}) }
    |> fun x -> c.save (x.at.ToString("r")) x 
    |> ignore
    {| name = name |}
    