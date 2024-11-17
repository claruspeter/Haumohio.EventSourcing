module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnit.Common
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open System.Collections.Generic
open TestCommon

let store = Haumohio.Storage.Memory.MemoryStore

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

[<Fact>]
let ``State can be loaded from an event within a partition`` () =
  Haumohio.Storage.Memory.resetAllData()
  let container = store.container "TEST"
  let response = storeEvent container "test1" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty
  let state = loadState "test1" container empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["42"]
  state.data.["42"].sum |> should equal 42

[<Fact>]
let ``State can be loaded from an event within a sub-partition`` () =
  Haumohio.Storage.Memory.resetAllData()
  let container = store.container "TEST"
  let response = storeEvent container "test1/sub1/sub2" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty
  let state = loadState "" container empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["42"]
  state.data.["42"].sum |> should equal 42
