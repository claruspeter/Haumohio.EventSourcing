module Tests

open System
open Xunit
open FsUnit.Xunit
open FsUnit.Common
open Haumohio.Storage
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open System.Collections.Generic
open TestCommon

let newStore() = Ephemeral.EphemeralStore Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance Store.StandardUtcProvider

[<Fact>]
let ``Event can be stored and retrieved`` () =
  let store = newStore()
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
  let store = newStore()
  let container = store.container "TEST"
  let response = storeEvent container "test1" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty 1
  let state = loadState "test1" container empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["42"]
  state.data.["42"].sum |> should equal 42

[<Fact>]
let ``State can be loaded from an event within a sub-partition`` () =
  let store = newStore()
  let container = store.container "TEST"
  let response = storeEvent container "test1/sub1/sub2" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty 1
  let state = loadState "" container empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["42"]
  state.data.["42"].sum |> should equal 42

[<Fact>]
let ``Events are stamped with timestamp according to time provider`` () =
  let fixedDate = DateTime(2021, 2, 3, 0, 0, 0, DateTimeKind.Utc)
  let store = {newStore() with timeProvider = fun () -> fixedDate}
  let container = store.container "Jo"
  let response = storeEvent container "flo" "yo" (Data 69)
  response.at |> should equal fixedDate

[<Fact>]
let ``Events are stored with timestamp according to time provider`` () =
  let fixedDate = DateTime(2021, 2, 3, 0, 0, 0, DateTimeKind.Utc)
  let store = {newStore() with timeProvider = fun () -> fixedDate}
  let container = store.container "Jo"
  let response = storeEvent container "flo" "yo" (Data 69)
  let ev = container.list "flo" |> Seq.head
  ev |> should haveSubstring "2021-02-03_00-00-00.000"
