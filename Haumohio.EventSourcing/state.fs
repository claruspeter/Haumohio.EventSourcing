namespace Haumohio.EventSourcing
open System
open System.Linq
open System.Collections.Generic
open Microsoft.Extensions.Logging

module StateStorage =
  open Haumohio.Storage
  open Haumohio
  open EventStorage
  open Projection

  let loadStateForDay<'K, 'S, 'D 
      when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S> and 'D: enum<int>> 
      (domain: 'D) (day:DateOnly) (container: EventSourcingContainer<'D>) =
    let filename = sprintf "%s/%s/%s.json" (domain.ToString().ToLowerInvariant()) (day.ToString "yyyyMMdd") (typeof<'S>.Name)
    container.stateContainer.loadAs<State<'K, 'S>> filename

  let rec makeStateForDay (domain: 'D) (day:DateOnly) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer<'D>) =
    match loadStateForDay domain (day.AddDays -1) container with 
    | Some state -> state
    | None -> 
        let initial = makeStateForDay domain (day.AddDays -1) projector container
        let state = projectForDay projector domain day initial container
        let filename = sprintf "%s/%s/%s.json" (domain.ToString().ToLowerInvariant()) (day.ToString "yyyyMMdd") (typeof<'S>.Name)
        let saved = container.stateContainer.save filename state
        state

  let makeState (domain: 'D) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer<'D>) =
    let today = container.stateContainer.timeProvider() |> DateOnly.FromDateTime 
    let yesterday = today |> _.AddDays(-1)
    let initial = 
      match loadStateForDay domain yesterday container with 
      | Some state -> state
      | None -> makeStateForDay domain yesterday projector container
    projectForDay projector domain today initial container


//   let makeState<'K, 'S, 'E when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> 
//       partition 
//       (container:StorageContainer) 
//       (policy : SnapshotPolicy)
//       (emptyState: State<'K,'S>) 
//       (projector: Projector<'K, 'S, 'E>) =



// module EventProjection =
//   open Haumohio.Storage
//   open Haumohio
//   open Projection

//   let loadLatestSnapshot<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality and 'P :> IEmpty<'P>> (partition:string) (container:StorageContainer): State<'K,'P> option =
//     match container.list(partition + "/" + typeof<'P>.Name) |> Seq.toList with 
//     | [] -> 
//       None
//     | xx -> 
//       let mostRecent = xx |> Seq.last 
//       sprintf "Loading snapshot %s from %s" (typeof<'P>.Name) mostRecent |> container.logger.LogDebug
//       let state =
//         mostRecent 
//         |> container.loadAs<State<'K,'P>>
//         |> Option.map (fun x -> x :> IAutoClean<State<'K, 'P>> |> _.clean() )
//       match state with 
//       | None -> None
//       | Some s ->
//         let cleaned = s.data |> Seq.map (fun x -> (x.Key, x.Value.clean())) |> fun x -> x.ToDictionary(fst, snd)
//         {s with data = cleaned}
//         |> Some

//   let loadVersionedSnapshot<'K, 'S when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S:equality and 'S :> IEmpty<'S>>
//         partition 
//         container 
//         (emptyState: State<'K, 'S>) =
//       match container |> loadLatestSnapshot partition with 
//       | None -> emptyState
//       | Some x when x.version < emptyState.version -> 
//         container.logger.LogWarning("State {state} version has increased to {version} - recalculating from events", typeof<'S>.Name, emptyState.version)
//         emptyState
//       | Some x -> x

//   let loadAfter<'E> partition (container:StorageContainer) (after: DateTime) =
//     let dtString = after |> EventStorage.dateString
//     let limit = $"event_{dtString}"
//     TimeSnap.snap $"loading events after {limit}"
//     container.filtered<'E> 
//       (if String.IsNullOrWhiteSpace(partition) then "" else partition + "/")
//       (fun x -> 
//         let fn = x.Split('/') |> Array.last
//         if fn.StartsWith("event") then
//           fn > limit
//         else
//           false
//       )
      

//   let loadStateFrom partition (container:StorageContainer) (initial: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
//     TimeSnap.snap $"Loaded state {partition} up to {initial.at}"
//     let events = loadAfter partition container initial.at |> Seq.sortBy (fun (x: Event<'E>) -> x.at) |> Seq.toArray
//     TimeSnap.snap $"loaded events ({events.Length})"
//     let final = project projector events initial
//     TimeSnap.snap $"projected state {partition}"
//     final

//   let loadState partition (container:StorageContainer) (emptyState: State<'K,'S>) (projector: Projector<'K, 'S, 'E>) =
//     TimeSnap.snap $"loadState({partition})"
//     let initial =loadVersionedSnapshot partition container emptyState
//     TimeSnap.snap $"loaded snapshot at {initial.at}"
//     loadStateFrom partition container initial projector

//   let saveState (partition:string) (container:StorageContainer) (state: State<'K,'S>) : State<'K,'S> =
//     let filename = 
//       sprintf "%s_%s"
//         (typeof<'S>.Name)
//         (container.timeProvider() |> EventStorage.dateString)
//     container.save $"{partition}/{filename}" state :?> _

//   let saveSingleState<'K, 'S when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> (partition:string) (container:StorageContainer) (single: 'S) version =
//     let now = container.timeProvider()
//     let latest = loadLatestSnapshot partition container
//     latest
//     |> Option.map (fun x -> x.[single.Key] = Some single )
//     |> function
//         | Some true -> latest.Value
//         | _ ->
//           let state = {data = [( single.Key, single )] |> dict; metadata=new Dictionary<string,string>(); at= now; version=version }
//           saveState partition container state

//   type SnapshotPolicy =
//     | Never
//     | EveryTime
//     | Daily
//     | Weekly

//   let makeState<'K, 'S, 'E when 'S :> IHasKey<'K> and 'S :> IAutoClean<'S> and 'S: equality and 'S :> IEmpty<'S>> 
//       partition 
//       (container:StorageContainer) 
//       (policy : SnapshotPolicy)
//       (emptyState: State<'K,'S>) 
//       (projector: Projector<'K, 'S, 'E>) =

//     TimeSnap.snap $"Make State at {partition}"
//     let initial = loadVersionedSnapshot partition container emptyState
//     let state = loadStateFrom partition container initial projector
//     match policy, state.at - initial.at with 
//     | Never, _ -> state
//     | EveryTime, x when x > TimeSpan.Zero -> saveState partition container state  // on every change, not every query
//     | Daily, x when x > TimeSpan.FromDays(1) -> saveState partition container state
//     | Weekly, x when x > TimeSpan.FromDays(7) -> saveState partition container state
//     | _ -> state