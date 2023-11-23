namespace Haumohio.EventSourcing
open System
open System.Collections.Generic
open Microsoft.Extensions.Logging

module Projection =
  open Haumohio.Storage
  open Haumohio

  type Projector<'S, 'E> = 'S -> Event<'E> -> 'S



  let project (projector: Projector<'S, 'E>) (events: Event<'E> seq)  (initialState:'S) =
    Seq.fold projector initialState events

  let loadLatestSnapshot (emptyState: 'S) (partition:string) (container:StorageContainer)  : (DateTime * 'S) =
    match container.list(partition + "/" + typeof<'S>.Name) |> Seq.toList with 
    | [] -> 
      (DateTime.MinValue, emptyState)
    | [x] -> 
      sprintf "Loading snapshot from %s" x |> container.logger.LogDebug
      (EventStorage.eventDate x,  container.loadAs<'S>(x).Value)
    | xx -> 
      let mostRecent = xx |> Seq.last 
      sprintf "Loading snapshot from %s" mostRecent |> container.logger.LogDebug
      (EventStorage.eventDate mostRecent,  mostRecent |> container.loadAs<'S> |> Option.get)

  let loadAfter<'E> partition (container:StorageContainer) (after: DateTime) =
    let dtString = after |> EventStorage.dateString
    let prefix = $"{partition}/event_{dtString}"
    container.list partition 
      |> Seq.filter (fun x -> x.Substring(0, prefix.Length) > prefix)
      |> Seq.choose container.loadAs<Event<'E>>

  let loadState<'S, 'E when 'E: comparison> partition (container:StorageContainer) (emptyState: 'S) (projector: Projector<'S, 'E>) : 'S =
    TimeSnap.snap "loadState()"
    let (since, initial) = container |> loadLatestSnapshot emptyState partition
    TimeSnap.snap "loaded snapshot"
    let events = loadAfter partition container since |> Seq.toArray
    TimeSnap.snap $"loaded events ({events.Length})"
    sprintf "Loading %d %s Events to project %s" (events |> Seq.length) partition (typeof<'S>.Name) |> container.logger.LogDebug
    let final = project projector events initial
    TimeSnap.snap "projected state"
    final 


  let saveState<'S> (partition:string) (container:StorageContainer) (state: 'S) : 'S =
    let filename = 
      sprintf "%s_%s"
        (typeof<'S>.Name)
        (Haumohio.Storage.Internal.UtcNow() |> EventStorage.dateString)
    container.save $"{partition}/{filename}" state :?> _