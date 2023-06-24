namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
  open Haumohio.Storage.Memory


  type Person = {
    name: string;
  }

  type PersonEvent =
    | Add of {| name: string |}

  let state = Haumohio.Storage.Memory.MemoryStore.container("state")

  let projector people event =
      match event.details with 
      | Add x -> people |> Seq.append [{Person.name = x.name}]

  let people() =
    let initial = state.all<Person>("")
    let events = state.all<Event<PersonEvent>>("");
    project projector events initial

  let addPerson (name:string) =
    { at = DateTime.UtcNow; by = "me"; details = (Add {|name=name|}) }
    |> fun x -> state.save (x.at.ToString("r")) x 
    |> ignore
    {| name = name |}
    