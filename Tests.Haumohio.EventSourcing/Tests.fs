module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnit.Common
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open System.Collections.Generic

let store = Haumohio.Storage.Memory.MemoryStore

type TestEvents = 
  | Data of amount: int
  | Other of string
with
  interface IHasDescription with
    member this.description: string =
      match this with 
      | Data amt -> $"DATA:{amt}"
      | Other s -> $"OTHER:{s}"


[<Fact>]
let ``Event can be stored and retrieved`` () =
  Haumohio.Storage.Memory.resetAllData()
  let container = store.container "TEST"
  let response = storeEvent container "test1" "test_user" (Data 42)
  let list = container.list "test1" 
  list |> Seq.length |> should equal 1
  let retrieved = container.loadAs<Event<TestEvents>> (list |> Seq.head)
  retrieved |> should not' (equal None)
  retrieved.Value.by |> should equal "test_user"
  match retrieved.Value.details with 
  | Data amt -> amt |> should equal 42
  | _ -> failwith "Not Data"


type TestProjection = {
  id: string
  sum: int
}with
  interface IHasKey<string> with 
    member this.Key = this.id

type TestState = Projection.State<string, TestProjection>

let private projector (state: TestState) (ev: Event<TestEvents>) =
  match ev.details with 
  | Data x -> 
    state.data.Add(KeyValuePair("A", {id="A"; sum=x}))
    state
  | _ -> state // do nothing 

[<Fact>]
let ``State can be loaded from and event within a partition`` () =
  Haumohio.Storage.Memory.resetAllData()
  let container = store.container "TEST"
  let response = storeEvent container "test1" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty
  let state = loadState "test1" container empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["A"]
  state.data.["A"].sum |> should equal 42

[<Fact>]
let ``State can be loaded from and event within a sub-partition`` () =
  Haumohio.Storage.Memory.resetAllData()
  let container = store.container "TEST"
  let response = storeEvent container "test1/sub1/sub2" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty
  let state = loadState "" container empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["A"]
  state.data.["A"].sum |> should equal 42