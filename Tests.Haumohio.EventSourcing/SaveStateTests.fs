module SaveStateTests

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

let testPartName = "save_state_test"

let now = DateTime.UtcNow

let SavePolicyData : obj[] list =[
  [|Never; 0; false |]
  [|Never; 1; false |]
  [|Never; 2; false |]
  [|Never; 7; false |]
  [|Never; 30; false |]
  [|EveryTime; 0; false |]
  [|EveryTime; 1; true |]
  [|EveryTime; 2; true |]
  [|EveryTime; 7; true |]
  [|EveryTime; 30; true |]
  [|Daily; 0; false |]
  [|Daily; 1; false |]
  [|Daily; 2; true |]
  [|Daily; 7; true |]
  [|Daily; 30; true |]
  [|Weekly; 0; false |]
  [|Weekly; 1; false |]
  [|Weekly; 2; false |]
  [|Weekly; 7; false |]
  [|Weekly; 8; true |]
  [|Weekly; 30; true |]  
]

let saveFakeStateAt (at: DateTime) (container:Haumohio.Storage.StorageContainer) (partition:string) : TestState =
  let state : TestState = {
    at = at
    data = new Dictionary<string, TestProjection>()
  }
  let filename = 
      sprintf "%s_%s"
        (typeof<TestProjection>.Name)
        (at |> EventStorage.dateString)
  container.save $"{partition}/{filename}" state :?> _

[<Theory>]
[<MemberData(nameof(SavePolicyData))>]
let ``State saved by policy`` policy offset (result:bool) =
  Haumohio.Storage.Memory.resetAllData()
  Haumohio.Storage.Internal.UtcNow <- fun () -> now
  let container = store.container "TEST"
  let prevDate = (now.AddDays(-offset))
  let previous = saveFakeStateAt prevDate container testPartName
  storeEvent container testPartName "test_user" (Data 1) |> ignore

  let state = makeState testPartName container policy empty projector

  state.data["1"].sum |> should equal 1
  container.list testPartName |> Seq.length |> (=) 3 |> should equal result
