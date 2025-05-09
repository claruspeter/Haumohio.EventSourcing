namespace Haumohio.EventSourcing
open System
open System.Linq
open System.Collections.Generic
open Microsoft.Extensions.Logging
open Projection

type StateStore<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality and 'P :> IEmpty<'P> and 'K:equality> = {
  /// <summary>A provider of the date & time</summary>
  timeProvider: unit -> DateTime
  /// <summary>A logger of messages</summary>
  logger: ILogger
  /// <summary>Save item by full name</summary>
  save: string -> State<'K,'P> -> State<'K,'P>
  /// <summary>List of items with prefix</summary>
  list: string -> string seq
  /// <summary>Load item by full name</summary>
  load: string -> State<'K,'P> option
}with 
  /// <summary>Load current by full name</summary>
  member this.latest (partition: string) =
    match this.list(partition + "/" + typeof<'P>.Name) |> Seq.toList with 
    | [] -> 
      None
    | xx -> 
      let mostRecent = xx |> Seq.last 
      sprintf "Loading snapshot from %s" mostRecent |> this.logger.LogDebug
      let state =
        mostRecent 
        |> this.latest
        |> Option.map (fun x -> x :> IAutoClean<State<'K, 'P>> |> _.clean() )
      match state with 
      | None -> None
      | Some s ->
        let cleaned = s.data |> Seq.map (fun x -> (x.Key, x.Value.clean())) |> fun x -> x.ToDictionary(fst, snd)
        {s with data = cleaned}
        |> Some




module ProjectionStorage =

  let loadVersionedSnapshot<'K, 'S when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S:equality and 'S :> IEmpty<'S>>
        partition 
        (store: StateStore<'K, 'S>)
        (emptyState: State<'K, 'S>) =
      match store.latest partition with 
      | None -> emptyState
      | Some x when x.version < emptyState.version -> 
        store.logger.LogWarning("State {state} version has increased to {version} - recalculating from events", typeof<'S>.Name, emptyState.version)
        emptyState
      | Some x -> x

  let loadStateFrom partition (events:EventStore<'E>) (initial: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
    TimeSnap.snap $"Loaded state up to {initial.at}"
    let events = EventStorage.since events partition initial.at |> Seq.sortBy (fun (x: Event<'E>) -> x.at) |> Seq.toArray
    TimeSnap.snap $"loaded events ({events.Length})"
    let final = project projector events initial
    TimeSnap.snap "projected state"
    final

  let loadState partition (store:StateStore<'K, 'S>) (events:EventStore<'E>) (emptyState: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
    TimeSnap.snap "loadState()"
    let initial =loadVersionedSnapshot partition store emptyState
    TimeSnap.snap $"loaded snapshot at {initial.at}"
    loadStateFrom partition events initial projector

  let saveState (partition:string) (store:StateStore<'K, 'S>) (state: State<'K,'S>) : State<'K,'S> =
    let filename = 
      sprintf "%s_%s"
        (typeof<'S>.Name)
        (store.timeProvider() |> EventStorage.dateString)
    store.save $"{partition}/{filename}" state

  let saveSingleState<'K, 'S when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> (partition:string) (store:StateStore<'K, 'S>) (single: 'S) version =
    let now = store.timeProvider()
    let latest = store.latest partition
    latest
    |> Option.map (fun x -> x.[single.Key] = Some single )
    |> function
        | Some true -> latest.Value
        | _ ->
          let state = {data = [( single.Key, single )] |> dict; metadata=new Dictionary<string,string>(); at= now; version=version }
          saveState partition store state

  type SnapshotPolicy =
    | Never
    | EveryTime
    | Daily
    | Weekly

  let makeState<'K, 'S, 'E when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> 
      partition 
      (store:StateStore<'K, 'S>) 
      (events: EventStore<'E>)
      (policy : SnapshotPolicy)
      (emptyState: State<'K,'S>) 
      (projector: Projector<'K, 'S, 'E>) =

    let initial = loadVersionedSnapshot partition store emptyState
    let state = loadStateFrom partition events initial projector
    match policy, state.at - initial.at with 
    | Never, _ -> state
    | EveryTime, x when x > TimeSpan.Zero -> saveState partition store state  // on every change, not every query
    | Daily, x when x > TimeSpan.FromDays(1) -> saveState partition store state
    | Weekly, x when x > TimeSpan.FromDays(7) -> saveState partition store state
    | _ -> state