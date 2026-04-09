module TrackingTests
open System
open Xunit
open FsUnit.Xunit
open Haumohio.Storage
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage
open Haumohio.EventSourcing.StateStorage
open TestCommon

type TestProjection = {
  key: int;
  title: string
  subItems: int list
}with 
  interface IHasKey<int> with 
    member this.Key = this.key
  interface IEmpty<TestProjection> with 
    static member empty = {key=0; title=String.Empty; subItems = []}
  interface IAutoClean<TestProjection> with 
    member this.clean() = this

type private TestState = State<int, TestProjection>

type TestEvent = 
  | CreateOne of {| title: string; trackingId: Guid |}
  | CreateSubItem of {| parentKey: int; trackingId: Guid |}
  interface IHasDescription with
      member this.description: string = 
        match this with 
        | CreateOne x -> $"Item created"
        | CreateSubItem x -> $"SubItem created in {x.parentKey}"

let private prefix = "test-item"

let private testProjector =
    fun (state: TestState) (ev: Event<TestEvent>) ->
      match ev.details with 
      | CreateOne x -> 
        let updatedState = state.incKey prefix x.trackingId
        addOrAmend (updatedState.nextKey prefix) (fun (p:TestProjection) -> { p with key=(updatedState.nextKey prefix); title=x.title; } ) updatedState
      | CreateSubItem x -> 
        let updatedState = state.incKey $"{prefix}_{x.parentKey}" x.trackingId
        let newKey = updatedState.nextKey $"{prefix}_{x.parentKey}"
        amend x.parentKey (fun (p:TestProjection) -> { p with subItems=p.subItems @ [newKey] } ) updatedState

type TestDomain = 
  | Tests = 1

let EventStore c = 
  {
    eventContainer=c
    stateContainer=c
  }

let _user = "test_user"
let _clientId = "test_user"

let newStore() = Ephemeral.EphemeralStore Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance incrementingTimeProvider



let addItem container title = 
  let trackingId = Guid.NewGuid()
  let response = 
    TestEvent.CreateOne {|title=title; trackingId = trackingId |}
    |> storeEvent container TestDomain.Tests _user
  response.action |> should equal "CreateOne"
  trackingId, response

let addSubItem container parentKey = 
  let trackingId = Guid.NewGuid()
  TestEvent.CreateSubItem {|parentKey=parentKey; trackingId = trackingId |}
    |> storeEvent container TestDomain.Tests _user
    |> _.action |> should equal "CreateSubItem"
  trackingId

[<Fact>]
let ``Creating first item gets key 1`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let _ = addItem container "My first Event"
  let state = makeState TestDomain.Tests testProjector container
  state.data.Count |> should equal 1
  let item = state.data.Values |> Seq.head
  item.key |> should equal 1

[<Fact>]
let ``Consecutive items get consecutive keys`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let _ = addItem container "My first Event"
  let _ = addItem container "My second Event"
  let _ = addItem container "My third Event"
  let _ = addItem container "My fourth Event"
  let state = makeState TestDomain.Tests testProjector container
  state.data.Values 
  |> Seq.sortBy _.key
  |> Seq.toList
  |> should equal [
    {key=1; title = "My first Event"; subItems=[] }
    {key=2; title = "My second Event"; subItems=[]}
    {key=3; title = "My third Event"; subItems=[]}
    {key=4; title = "My fourth Event"; subItems=[]}
  ]

[<Fact>]
let ``The next key is the latest assigned key`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let _ = addItem container "My first Event"
  let _ = addItem container "My second Event"
  let _ = addItem container "My third Event"
  let _ = addItem container "My fourth Event"
  let state = makeState TestDomain.Tests testProjector container
  state.nextKey prefix |> should equal 4

[<Fact>]
let ``Incrementing the state increments the next key`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let _ = addItem container "My first Event"
  let _ = addItem container "My second Event"
  let _ = addItem container "My third Event"
  let _ = addItem container "My fourth Event"
  let state = makeState TestDomain.Tests testProjector container
  let incremented = state.incKey prefix (Guid.NewGuid())
  incremented.nextKey prefix |> should equal 5

[<Fact>]
let ``Sub-items may have independent calculations`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let _ = addItem container "My first Event"
  let _ = addSubItem container 1
  let _ = addItem container "My second Event"
  let _ = addSubItem container 2
  let _ = addSubItem container 1
  let state = makeState TestDomain.Tests testProjector container
  let a = state.[1].Value
  let b = state.[2].Value
  a.subItems |> should equal [1; 2]
  b.subItems |> should equal [1]

[<Fact>]
let ``Creating an item tracks the key`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let trackingId, response = addItem  container "My first Event"
  let state = makeState TestDomain.Tests testProjector container
  state |> lookupTrackedKey trackingId |> should equal (Some 1)

[<Fact>]
let ``Looking up an non-matching trackingId returns None`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let trackingId, response = addItem  container "My first Event"
  let state = makeState TestDomain.Tests testProjector container
  state |> lookupTrackedKey (Guid.NewGuid()) |> should equal None

[<Fact>]
let ``Response from creating an item includes the new key in the category`` () =
  let store = newStore()
  let container : EventSourcingContainer = store.container _clientId |> EventStore
  let trackingId, response = addItem  container "My first Event"
  let state = makeState TestDomain.Tests testProjector container
  let trackedResponse = addKeyToEventStorageResponse trackingId state response
  trackedResponse.domain |> should equal "tests/1"

[<Fact>]
let ``Tracked event storage response calculates new key on demand`` () =
  let store = newStore()
  let container = store.container _clientId |> EventStore
  let trackingId = Guid.NewGuid()
  let stateGen = fun c -> makeState TestDomain.Tests testProjector c
  let response : CreateEventStorageResponse = 
    TestEvent.CreateOne {|title="My first event"; trackingId = trackingId |}
    |> storeTrackedEvent container TestDomain.Tests trackingId stateGen _user
  response.action |> should equal "CreateOne"
  response.at |> should equal _test_now
  response.by |> should equal _user
  response.description |> should equal "Item created"
  response.domain |> should equal "tests"
  response.GenerateKey container |> should equal "1"
