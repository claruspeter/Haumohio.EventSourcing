module SaveStateTests

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


let saveFakeStateAt (at: DateTime) (store: StateStore<string, TestProjection>) (partition:string) : TestState =
  let state : TestState = {
    at = at
    data = new Dictionary<string, TestProjection>()
    metadata = new Dictionary<string, string>()
    version = 1
  }
  let filename = 
      sprintf "%s_%s"
        (typeof<TestProjection>.Name)
        (at |> EventStorage.dateString)
  store.save $"{partition}/{filename}" state

[<Theory>]
[<MemberData(nameof(SavePolicyData))>]
let ``State saved by policy`` policy offset (result:bool) =
  let events = {EphemeralEventStore() with timeProvider = fun () -> now}
  let states = {EphemeralStateStore() with timeProvider = fun () -> now}
  let prevDate = (now.AddDays(-offset))
  let previous = saveFakeStateAt prevDate states testPartName
  storeEvent events testPartName "test_user" (Data 1) |> ignore
  let state = makeState testPartName states events policy empty projector
  states.list testPartName |> Seq.length |> (=) 3 |> should equal result

[<Fact>]
let ``State can auto clean``() =
  let json = "{ 'sum': 3, 'id': 'Fred' }"
  let result = Newtonsoft.Json.JsonConvert.DeserializeObject<TestProjection>(json)
  result.sum |> should equal 3
  result.id |> should equal "Fred"
  result.stuff |> should equal null
  let cleaned = (result :> IAutoClean<TestProjection>).clean()
  cleaned.stuff |> should matchList []
