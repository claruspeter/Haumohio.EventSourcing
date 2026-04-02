module StateStorageTests

open System
open Microsoft.Extensions.Logging
open Xunit
open FsUnit.Xunit
open Haumohio.Storage
open Haumohio.EventSourcing
open Haumohio.EventSourcing.EventStorage
open TestCommon
open Haumohio.EventSourcing.Projection


let _now = fun () -> DateTime(2006, 6,5,4,3,2,1)
let _today = _now() |> DateOnly.FromDateTime

let _logger = LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore).CreateLogger("EventStorageTests")
let newStore() = Ephemeral.EphemeralStore _logger _now

let setTime container at =
  {container with eventContainer = {container.eventContainer with timeProvider = fun () -> at}}


type TestDomain = 
    | Test1 = 1
    | Flo = 2

let EventStore c = 
  {
    eventContainer=c
    stateContainer=c
  }

[<Fact>]
let ``The initial event date is calculated from the first event`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let cYesterday = setTime container (_now().AddDays(-1))
  let _ = storeEvent cYesterday TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime cYesterday (_now())
  let _ = storeEvent cToday TestDomain.Test1 "test_user" (Data 43)
  //Act
  let date = StateStorage.findInitialEventDate TestDomain.Test1 cToday
  //Assert
  date |> should equal (Some (_today.AddDays -1))

[<Fact>]
let ``When there are no events the initial event date is None`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  //Act
  let date = StateStorage.findInitialEventDate TestDomain.Test1 container
  //Assert
  date |> should equal None

[<Fact>]
let ``State is stored in the state container under it's own folder`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let cYesterday = setTime container (_now().AddDays(-1))
  let _ = storeEvent cYesterday TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime cYesterday (_now())
  //Act
  let _ = StateStorage.makeState TestDomain.Test1 projector cToday
  //Assert
  cToday.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/TestProjection/2006-06-04"]

[<Fact>]
let ``State is calculated but not stored for today`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let _ = storeEvent container TestDomain.Test1 "test_user" (Data 42)
  //Act
  let state = StateStorage.makeState TestDomain.Test1 projector container
  //Assert
  container.stateContainer.list "test1" |> Seq.length |> should equal 0
  state.["42"].IsSome |> should equal true

[<Fact>]
let ``State is stored for each day leading up to today`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let c10 = setTime container (_now().AddDays(-10))
  let _ = storeEvent c10 TestDomain.Test1 "test_user" (Data 42)
  let c05 = setTime c10 (_now().AddDays(-5))
  let _ = storeEvent c05 TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime c05 (_now())
  //Act
  let _ = StateStorage.makeState TestDomain.Test1 projector cToday
  //Assert
  cToday.stateContainer.list "test1" 
  |> Seq.toList 
  |> should equal [
    "test1/TestProjection/2006-05-26"
    "test1/TestProjection/2006-05-27"
    "test1/TestProjection/2006-05-28"
    "test1/TestProjection/2006-05-29"
    "test1/TestProjection/2006-05-30"
    "test1/TestProjection/2006-05-31"
    "test1/TestProjection/2006-06-01"
    "test1/TestProjection/2006-06-02"
    "test1/TestProjection/2006-06-03"
    "test1/TestProjection/2006-06-04"
  ]

[<Fact>]
let ``State is calculated for each day leading up to today`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let c10 = setTime container (_now().AddDays(-10))
  let _ = storeEvent c10 TestDomain.Test1 "test_user" (Data 42)
  let c05 = setTime c10 (_now().AddDays(-5))
  let _ = storeEvent c05 TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime c05 (_now())
  //Act
  let _ = StateStorage.makeState TestDomain.Test1 projector cToday
  //Assert
  cToday.stateContainer.list "test1" 
  |> Seq.toList
  |> Seq.map (cToday.stateContainer.loadAs<TestState> >> _.Value.["42"].Value)
  |> Seq.map _.cnt
  |> Seq.toList
  |> should equal [1; 1; 1; 1; 1; 2; 2; 2; 2; 2]

[<Fact>]
let ``State can be calculated using a given empty state`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let _ = storeEvent container TestDomain.Test1 "test_user" (Data 42)
  let initial = TestState.empty |> addOrAmend "42" (fun x -> {x with cnt=10; stuff=[1;2;3]})
  //Act
  let state = StateStorage.makeStateWithEmpty initial TestDomain.Test1 projector container
  //Assert
  container.stateContainer.list "test1" |> Seq.length |> should equal 0
  state.["42"].IsSome |> should equal true
  let result = state.["42"].Value
  result.cnt |> should equal 11
  result.stuff |> should equal [1;2;3]