namespace Haumohio.EventSourcing
open System

module Projection =
  open Haumohio.Storage
  type Projector<'S, 'E> = 'S -> Event<'E> -> 'S

  let project (projector: Projector<'S, 'E>) (events: Event<'E> seq)  (initialState:'S) =
    Seq.fold projector initialState events

  let loadLatestSnapshot (emptyState: 'S) (partition:string) (container:StorageContainer)  : 'S =
    match container.list(partition + "/" + typeof<'S>.Name) |> Seq.toList with 
    | [] -> 
      emptyState
    | [x] -> 
      container.loadAs<'S>(x).Value
    | xx -> 
      xx |> Seq.last |> container.loadAs<'S> |> Option.get

  let loadState<'S, 'E> partition (container:StorageContainer) (emptyState: 'S) (projector: Projector<'S, 'E>) : 'S =
    let initial = container |> loadLatestSnapshot emptyState partition
    //printfn "Initial state: %A" initial
    let events = container.all<Event<'E>>($"{partition}/event")
    printfn "Loading %d %s Events to project %s" (events |> Seq.length) partition (typeof<'S>.Name)
    let final = project projector events initial
    //printfn "Final State: %A"  final
    final 
