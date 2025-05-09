module TestCommon
open System
open System.Collections.Generic
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open Haumohio.EventSourcing.ProjectionStorage

type TestEvents = 
  | Data of amount: int
  | Other of string
with
  interface IHasDescription with
    member this.description: string =
      match this with 
      | Data amt -> $"DATA:{amt}"
      | Other s -> $"OTHER:{s}"

type TestProjection = {
  id: string
  sum: int
  stuff: int list
}with
  interface IHasKey<string> with 
    member this.Key = this.id
  interface IEmpty<TestProjection> with 
    static member empty = {id=""; sum=0; stuff = []}
  interface IAutoClean<TestProjection> with 
    member this.clean() = 
      {this with 
        stuff = unNull [] this.stuff
      }

type TestState = Projection.State<string, TestProjection>

let projector (state: TestState) (ev: Event<TestEvents>) =
  match ev.details with 
  | Data x -> 
    state.data.[x.ToString()] <- {id=x.ToString(); sum=x; stuff=[]}
    state
  | _ -> state // do nothing 

let empty = State<string, TestProjection>.empty 1


let EphemeralEventStore() : EventStore<'TEvent> =
  let d = new Dictionary<string, Event<'TEvent>>()
  {
    logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
    timeProvider = fun() -> DateTime.UtcNow
    save = fun key value -> 
      d.[key] <- value
      Threading.Thread.Sleep(1) // entry named to the ms.  It can cause collisions
      value
    list = fun prefix ->
      d.Keys
      |> Seq.filter (fun x -> x.StartsWith prefix)
    load = fun prefix predicate -> 
      d
      |> Seq.filter (fun x -> x.Key.StartsWith prefix)
      |> Seq.filter (fun x -> predicate x.Key)
      |> Seq.map (fun x -> x.Value)
  }
let EphemeralStateStore() : StateStore<string, TestProjection> = 
  let d = new Dictionary<string, State<string, TestProjection>>()

  {
    logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
    timeProvider = fun() -> DateTime.UtcNow
    save = fun key value -> 
      d.[key] <- value
      value
    list = fun prefix ->
      d.Keys
      |> Seq.filter (fun x -> x.StartsWith prefix)
    load = fun name -> 
      if d.ContainsKey name then 
        d.[name] |> Some
      else
        None
  }
