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
