module Tests

open System
open Xunit
open FsUnit.Xunit
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open Haumohio.EventSourcing.ProjectionStorage
open TestCommon

[<Fact>]
let ``Event can be stored and retrieved`` () =
  let events = EphemeralEventStore()
  let states = EphemeralStateStore()
  let response = storeEvent events "test1" "test_user" (Data 42)
  let retrieved = events.load "test1" (fun _ -> true) |> Seq.toList
  retrieved |> List.length |> should equal 1
  retrieved.[0].by |> should equal "test_user"
  match retrieved.[0].details with 
  | Data amt -> amt |> should equal 42
  | _ -> failwith "Not Data"

[<Fact>]
let ``State can be loaded from an event within a partition`` () =
  let events = EphemeralEventStore()
  let states = EphemeralStateStore()
  let response = storeEvent events "test1" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty 1
  let state = loadState "test1" states events empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["42"]
  state.data.["42"].sum |> should equal 42

[<Fact>]
let ``State can be loaded from an event within a sub-partition`` () =
  let events = EphemeralEventStore()
  let states = EphemeralStateStore()
  let response = storeEvent events "test1/sub1/sub2" "test_user" (Data 42)
  let empty = State<string, TestProjection>.empty 1
  let state = loadState "" states events empty projector
  state.data.Keys |> Seq.toList |> should equalSeq ["42"]
  state.data.["42"].sum |> should equal 42

[<Fact>]
let ``Events are stamped with timestamp according to time provider`` () =
  let fixedDate = DateTime(2021, 2, 3, 0, 0, 0, DateTimeKind.Utc)
  let events = {EphemeralEventStore() with timeProvider = fun () -> fixedDate}
  let response = storeEvent events "flo" "yo" (Data 69)
  response.at |> should equal fixedDate

[<Fact>]
let ``Events are stored with timestamp according to time provider`` () =
  let fixedDate = DateTime(2021, 2, 3, 0, 0, 0, DateTimeKind.Utc)
  let events = {EphemeralEventStore() with timeProvider = fun () -> fixedDate}
  let response = storeEvent events "flo" "yo" (Data 69)
  let ev = events.list "flo" |> Seq.head
  ev |> should haveSubstring "2021-02-03_00-00-00.000"
