namespace Haumohio.EventSourcing
open System

module Projection =
  open Haumohio.Storage
  type Projector<'S, 'E> = 'S -> Event<'E> -> 'S

  let project (projector: Projector<'S, 'E>) (events: Event<'E> seq)  (initialState:'S) =
    Seq.fold projector initialState events

  let loadState<'S, 'E> (container:StorageContainer) (emptyState: 'S) (projector: Projector<'S, 'E>) : 'S =
    let initial = 
      match container.list(typeof<'S>.Name) |> Seq.toList with 
      | [] -> 
        emptyState
      | [x] -> 
        container.loadAs<'S>(x).Value
      | xx -> 
        xx |> Seq.last |> container.loadAs<'S> |> Option.get
    let events = container.all<Event<'E>>("");
    project projector events initial
