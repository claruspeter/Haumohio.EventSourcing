module StateVersioningTests

open System
open Xunit
open FsUnit.Xunit
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open Haumohio.EventSourcing.ProjectionStorage
open System.Collections.Generic
open TestCommon

let testPartName = "save_state_test"
let now = DateTime.UtcNow
let currentVersion = 3
let savedStateValue = 42

let setTime (store: StateStore<string,TestProjection>) at =
  {store with timeProvider = fun () -> at}

let start() =
  let states = EphemeralStateStore()
  let events = EphemeralEventStore()
  let yesterday = setTime states (now.AddDays -1)
  storeEvent events "TEST" "Fred" (Data 99) |> ignore
  let lastMinute = setTime yesterday (now.AddMinutes -1)
  let savedState = saveSingleState "TEST" lastMinute {id="42"; sum=savedStateValue; stuff=[]} currentVersion
  lastMinute, events

[<Fact>]
let ``State without version autocleans to version 0``() =
  let stateNull: TestState = {
    at = DateTime.UtcNow
    data = new Dictionary<string, TestProjection>()
    metadata = new Dictionary<string,string>()
    version = Unchecked.defaultof<int>
  }
  let cleaned = stateNull :> IAutoClean<TestState> |> _.clean()
  cleaned.version |> should equal 0

[<Fact>]
let ``Loading snapshot gets version``() =
  let states, events = start()
  let snapshot = states.latest "TEST"
  snapshot.IsSome |> should equal true
  snapshot.Value.version |> should equal currentVersion
  
[<Fact>]
let ``State uses snapshots that are the requested version``() =
  let states, events = start()
  let state = loadState "TEST" states events (TestState.empty currentVersion) projector
  state.version |> should equal currentVersion
  state["42"].Value.sum |> should equal savedStateValue

[<Fact>]
let ``State ignores snapshots lower than requested version``() =
  let states, events = start()
  let state = loadState "TEST" states events (TestState.empty (currentVersion + 1)) projector
  state.version |> should equal (currentVersion + 1)
  state.data.Keys |> should haveCount 1
  state["99"].Value.sum |> should equal 99

[<Fact>]
let ``Make State uses snapshots that are the requested version``() =
  let states, events = start()
  let state = makeState "TEST" states events SnapshotPolicy.EveryTime (TestState.empty currentVersion) projector
  state.version |> should equal currentVersion
  state["42"].Value.sum |> should equal savedStateValue

[<Fact>]
let ``Make State ignores snapshots lower than requested version``() =
  let states, events = start()
  let state = makeState "TEST" states events SnapshotPolicy.EveryTime (TestState.empty (currentVersion + 1)) projector
  state.version |> should equal (currentVersion + 1)
  state.data.Keys |> should haveCount 1
  state["99"].Value.sum |> should equal 99
