namespace Haumohio.EventSourcing
open System
open System.Collections.Generic
open Microsoft.Extensions.Logging

module Projection =
  open Haumohio.Storage
  open Haumohio

  type IHasKey<'T when 'T: equality> = 
    abstract member Key : 'T

  type State<'Key, 'Model when 'Key: equality and 'Model :> IHasKey<'Key>> = {
    data: IDictionary<'Key, 'Model>
    at: DateTime
  }with 
    static member empty = { data = new Dictionary<'Key, 'Model>(); at = DateTime.MinValue}
    member this.Item with get (key:'Key) = 
      match this.data.TryGetValue key with 
      | true, x -> Some x
      | _ -> None

  type Projector<'K, 'S, 'E when 'K: equality and 'S :> IHasKey<'K>> = State<'K,'S> -> Event<'E> -> State<'K,'S>

  let project (projector: Projector<'K, 'S, 'E>) (events: Event<'E> seq)  (initialState:State<'K,'S>) =
    Seq.fold projector initialState events

  let loadLatestSnapshot (emptyState: State<'K,'S>) (partition:string) (container:StorageContainer) =
    match container.list(partition + "/" + typeof<'S>.Name) |> Seq.toList with 
    | [] -> 
      emptyState
    | [x] -> 
      sprintf "Loading snapshot from %s" x |> container.logger.LogDebug
      container.loadAs<State<'K,'S>>(x).Value
    | xx -> 
      let mostRecent = xx |> Seq.last 
      sprintf "Loading snapshot from %s" mostRecent |> container.logger.LogDebug
      mostRecent |> container.loadAs<State<'K,'S>> |> Option.get

  let loadAfter<'E> partition (container:StorageContainer) (after: DateTime) =
    let dtString = after |> EventStorage.dateString
    let prefix = $"{partition}/event_{dtString}"
    container.list partition 
      |> Seq.filter (fun x -> x.Contains("/event_") &&  x.Substring(0, prefix.Length) > prefix)
      |> Seq.choose container.loadAs<Event<'E>>

  let loadState partition (container:StorageContainer) (emptyState: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
    TimeSnap.snap "loadState()"
    let  initial = container |> loadLatestSnapshot emptyState partition
    TimeSnap.snap $"loaded snapshot at {initial.at}"
    let events = loadAfter partition container initial.at |> Seq.toArray
    TimeSnap.snap $"loaded events ({events.Length})"
    sprintf "Loading %d %s Events to project %s" (events |> Seq.length) partition (typeof<'S>.Name) |> container.logger.LogDebug
    let final = project projector events initial
    TimeSnap.snap "projected state"
    final 


  let saveState (partition:string) (container:StorageContainer) (state: State<'K,'S>) : State<'K,'S> =
    let filename = 
      sprintf "%s_%s"
        (typeof<'S>.Name)
        (Haumohio.Storage.Internal.UtcNow() |> EventStorage.dateString)
    container.save $"{partition}/{filename}" state :?> _

  let saveSingleState<'K, 'S when 'S :> IHasKey<'K> > (partition:string) (container:StorageContainer) (single: 'S) =
    let state = {data = [( single.Key, single )] |> dict; at=Storage.Internal.UtcNow() }
    saveState partition container state