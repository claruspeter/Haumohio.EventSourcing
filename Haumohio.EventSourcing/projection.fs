namespace Haumohio.EventSourcing
open System
open System.Linq
open System.Collections.Generic
open Microsoft.Extensions.Logging

#nowarn "3535"
type IEmpty<'P> =
  static abstract member empty: 'P

type IAutoClean<'a> =
  abstract member clean : unit -> 'a

module Projection =
  open Haumohio.Storage
  open Haumohio

  let inline unNull defaultValue value =
    match value |> box with 
    | null -> defaultValue
    | _ -> value

  let inline autoClean<'a when 'a :> IAutoClean<'a> > (state: 'a) =
    (state :> IAutoClean<'a>).clean()

  type IHasKey<'T when 'T: equality> = 
    abstract member Key : 'T

  type State<'Key, 'Model when 'Key: equality and 'Model :> IHasKey<'Key> and 'Model :> IAutoClean<'Model> and 'Model: equality> = {
    data: IDictionary<'Key, 'Model>
    metadata: IDictionary<string, string>
    at: DateTime
    version: int
  }with 
    static member empty version = {
      data = new Dictionary<'Key, 'Model>();
      metadata = new Dictionary<string, string>();
      at = DateTime.MinValue; version=version;
    }
    member this.Item with get (key:'Key) = 
      match this.data.TryGetValue key with 
      | true, x -> Some x
      | _ -> None
    interface IAutoClean<State<'Key,'Model>> with 
      member this.clean (): State<'Key,'Model> = 
        if this.version = Unchecked.defaultof<int> then
          {this with version = 0}
        else
          this

  type Projector<'K, 'S, 'E when 'K: equality and 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality> = State<'K,'S> -> Event<'E> -> State<'K,'S>

  let project (projector: Projector<'K, 'S, 'E>) (events: Event<'E> seq)  (initialState:State<'K,'S>) =
    let final = Seq.fold projector initialState events
    if events |> Seq.isEmpty  then 
      final
    else
      {final with at= events |> Seq.last |> fun x -> x.at }

  let amend<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality> (key: 'K) (updater: 'P -> 'P) (state: State<'K, 'P>) =
    match state.data.ContainsKey key with 
    | true ->
        state.data.[key] <- state.data.[key] |> updater
        state
    | false -> 
      printfn "Can't Amend - key not found %A" key
      state
  
  let addOrAmend<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality and 'P :> IEmpty<'P>> key (updater: 'P -> 'P) (state: State<'K, 'P>) =
    match key |> state.data.ContainsKey |> not with
    | true ->
        state.data.[key] <- 'P.empty |> updater
        state
    | false ->
      amend key updater state

  let setMetaData key value state =
    state.metadata[key] <- value
    state

  let loadLatestSnapshot<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality and 'P :> IEmpty<'P>> (partition:string) (container:StorageContainer): State<'K,'P> option =
    match container.list(partition + "/" + typeof<'P>.Name) |> Seq.toList with 
    | [] -> 
      None
    | xx -> 
      let mostRecent = xx |> Seq.last 
      sprintf "Loading snapshot from %s" mostRecent |> container.logger.LogDebug
      let state =
        mostRecent 
        |> container.loadAs<State<'K,'P>>
        |> Option.map (fun x -> x :> IAutoClean<State<'K, 'P>> |> _.clean() )
      match state with 
      | None -> None
      | Some s ->
        let cleaned = s.data |> Seq.map (fun x -> (x.Key, x.Value.clean())) |> fun x -> x.ToDictionary(fst, snd)
        {s with data = cleaned}
        |> Some

  let loadVersionedSnapshot<'K, 'S when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S:equality and 'S :> IEmpty<'S>>
        partition 
        container 
        (emptyState: State<'K, 'S>) =
      match container |> loadLatestSnapshot partition with 
      | None -> emptyState
      | Some x when x.version < emptyState.version -> 
        container.logger.LogWarning("State {state} version has increased to {version} - recalculating from events", typeof<'S>.Name, emptyState.version)
        emptyState
      | Some x -> x

  let loadAfter<'E> partition (container:StorageContainer) (after: DateTime) =
    let dtString = after |> EventStorage.dateString
    let limit = $"event_{dtString}_zzzzzzz"
    TimeSnap.snap $"loading events after {limit}"
    container.filtered<'E> 
      partition
      (fun x -> 
        let fn = x.Split('/') |> Array.last
        if fn.StartsWith("event") then
          fn > limit
        else
          false
      )
      

  let loadStateFrom partition (container:StorageContainer) (initial: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
    TimeSnap.snap $"Loaded state up to {initial.at}"
    let events = loadAfter partition container initial.at |> Seq.sortBy (fun (x: Event<'E>) -> x.at) |> Seq.toArray
    TimeSnap.snap $"loaded events ({events.Length})"
    let final = project projector events initial
    TimeSnap.snap "projected state"
    final

  let loadState partition (container:StorageContainer) (emptyState: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
    TimeSnap.snap "loadState()"
    let initial =loadVersionedSnapshot partition container emptyState
    TimeSnap.snap $"loaded snapshot at {initial.at}"
    loadStateFrom partition container initial projector

  let saveState (partition:string) (container:StorageContainer) (state: State<'K,'S>) : State<'K,'S> =
    let filename = 
      sprintf "%s_%s"
        (typeof<'S>.Name)
        (Haumohio.Storage.Internal.UtcNow() |> EventStorage.dateString)
    container.save $"{partition}/{filename}" state :?> _

  let saveSingleState<'K, 'S when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> (partition:string) (container:StorageContainer) (single: 'S) version =
    let now = Storage.Internal.UtcNow()
    let latest = loadLatestSnapshot partition container
    latest
    |> Option.map (fun x -> x.[single.Key] = Some single )
    |> function
        | Some true -> latest.Value
        | _ ->
          let state = {data = [( single.Key, single )] |> dict; metadata=new Dictionary<string,string>(); at= now; version=version }
          saveState partition container state

  type SnapshotPolicy =
    | Never
    | EveryTime
    | Daily
    | Weekly

  let makeState<'K, 'S, 'E when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> 
      partition 
      (container:StorageContainer) 
      (policy : SnapshotPolicy)
      (emptyState: State<'K,'S>) 
      (projector: Projector<'K, 'S, 'E>) =

    let initial = loadVersionedSnapshot partition container emptyState
    let state = loadStateFrom partition container initial projector
    match policy, state.at - initial.at with 
    | Never, _ -> state
    | EveryTime, x when x > TimeSpan.Zero -> saveState partition container state  // on every change, not every query
    | Daily, x when x > TimeSpan.FromDays(1) -> saveState partition container state
    | Weekly, x when x > TimeSpan.FromDays(7) -> saveState partition container state
    | _ -> state