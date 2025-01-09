module StateVersioningTests

open System
open Xunit
open FsUnit.Xunit
open FsUnit.Common
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open System.Collections.Generic
open TestCommon

let store = Haumohio.Storage.Ephemeral.EphemeralStore()
let testPartName = "save_state_test"
let now = DateTime.UtcNow
let currentVersion = 3
let savedStateValue = 42

let setTime at =
  Haumohio.Storage.Internal.UtcNow <- fun () -> at

let start() =
  let store = Haumohio.Storage.Ephemeral.EphemeralStore()
  let container = store.container (Guid.NewGuid().ToString())
  setTime (now.AddDays -1)
  storeEvent container "TEST" "Fred" (Data 99) |> ignore
  setTime (now.AddMinutes -1)
  let savedState = saveSingleState "TEST" container {id="42"; sum=savedStateValue; stuff=[]} currentVersion
  container

[<Fact>]
let ``State without version autocleans to version 0``() =
  let stateNull: TestState = {
    at = DateTime.UtcNow
    data = new Dictionary<string, TestProjection>()
    version = Unchecked.defaultof<int>
  }
  let cleaned = stateNull :> IAutoClean<TestState> |> _.clean()
  cleaned.version |> should equal 0

[<Fact>]
let ``Loading snapshot gets version``() =
  let container = start()
  let snapshot = loadLatestSnapshot<string, TestProjection> "TEST" container
  snapshot.IsSome |> should equal true
  snapshot.Value.version |> should equal currentVersion
  
[<Fact>]
let ``State uses snapshots that are the requested version``() =
  let container = start()
  let state = loadState "TEST" container (TestState.empty currentVersion) projector
  state.version |> should equal currentVersion
  state["42"].Value.sum |> should equal savedStateValue

[<Fact>]
let ``State ignores snapshots lower than requested version``() =
  let container = start()
  let state = loadState "TEST" container (TestState.empty (currentVersion + 1)) projector
  state.version |> should equal (currentVersion + 1)
  state.data.Keys |> should haveCount 1
  state["99"].Value.sum |> should equal 99

[<Fact>]
let ``Make State uses snapshots that are the requested version``() =
  let container = start()
  let state = makeState "TEST" container SnapshotPolicy.EveryTime (TestState.empty currentVersion) projector
  state.version |> should equal currentVersion
  state["42"].Value.sum |> should equal savedStateValue

[<Fact>]
let ``Make State ignores snapshots lower than requested version``() =
  let container = start()
  let state = makeState "TEST" container SnapshotPolicy.EveryTime (TestState.empty (currentVersion + 1)) projector
  state.version |> should equal (currentVersion + 1)
  state.data.Keys |> should haveCount 1
  state["99"].Value.sum |> should equal 99
