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
  open EventStorage
  open System.Collections.Immutable

  let inline unNull defaultValue value =
    match value |> box with 
    | null -> defaultValue
    | _ -> value

  let inline autoClean<'a when 'a :> IAutoClean<'a> > (state: 'a) =
    (state :> IAutoClean<'a>).clean()

  type IHasKey<'T when 'T: equality> = 
    abstract member Key : 'T

  type State<'Key, 'Model 
      when 'Key: equality 
      and 'Model :> IHasKey<'Key> 
      and 'Model :> IAutoClean<'Model> 
      and 'Model: equality
      > = {
    data: IImmutableDictionary<'Key, 'Model>
    metadata: IDictionary<string, string>
    at: DateTime
    version: int
  }with 
    static member empty = {
      data = ImmutableDictionary<'Key, 'Model>.Empty;
      metadata = new Dictionary<string, string>();
      at = DateTime.MinValue; version=1;
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
    interface IEmpty<State<'Key,'Model>> with 
      static member empty = State<'Key,'Model>.empty

  type Projector<'K, 'S, 'E 
      when 'K: equality 
      and 'S :> IHasKey<'K> 
      and 'S :> IAutoClean<'S> 
      and 'S: equality
    > = State<'K,'S> -> Event<'E> -> State<'K,'S>

  let private projectEvents (projector: Projector<'K, 'S, 'E>) (events: Event<'E> seq)  (initialState:State<'K,'S>) =
    let final = Seq.fold projector initialState events
    if events |> Seq.isEmpty  then 
      final
    else
      {final with at= events |> Seq.last |> fun x -> x.at }

  let projectForDay (projector: Projector<'K, 'S, 'E>) (domain:'D) (day: DateOnly) initialState (container: EventStorage.EventSourcingContainer<'D>) =
    let events = 
      sprintf "%s/event/%s" (domain.ToString().ToLowerInvariant()) (day |> dateOnlyString)
      |> container.eventContainer.all<Event<'E>>
    projectEvents projector events initialState

  let project (projector: Projector<'K, 'S, 'E>) (domain:'D) initialState (container: EventStorage.EventSourcingContainer<'D>) =
    projectForDay projector domain (container.timeProvider() |> DateOnly.FromDateTime ) initialState container

  let amend<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality> (key: 'K) (updater: 'P -> 'P) (state: State<'K, 'P>) =
    match state.data.ContainsKey key with 
    | true ->
        {state with data=state.data.SetItem(key,  state.data.[key] |> updater)}
    | false -> 
      printfn "Can't Amend - key not found %A" key
      state
  
  let addOrAmend<'K, 'P when 'P :> IHasKey<'K> and 'P :> IAutoClean<'P> and 'P:equality and 'P :> IEmpty<'P>> key (updater: 'P -> 'P) (state: State<'K, 'P>) =
    match key |> state.data.ContainsKey |> not with
    | true ->
        {state with data=state.data.Add(key, 'P.empty |> updater)}
    | false ->
      amend key updater state

  let setMetaData key value state =
    state.metadata[key] <- value
    state
