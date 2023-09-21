namespace Haumohio.EventSourcing
open System
open Microsoft.Extensions.Logging

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
    let events = container.all<Event<'E>>($"{partition}/event")
    sprintf "Loading %d %s Events to project %s" (events |> Seq.length) partition (typeof<'S>.Name) |> container.logger.LogDebug
    let final = project projector events initial
    final 

  let loadAfter<'E> partition (container:StorageContainer) (after: DateTime) =
    let dtString = after |> EventStorage.dateString
    let prefix = $"{partition}/event_{dtString}"
    container.list partition 
      |> Seq.filter (fun x -> x.Substring(0, prefix.Length) > prefix)
      |> Seq.choose container.loadAs<Event<'E>>