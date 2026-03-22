module StateStorageTests

open System
open Microsoft.Extensions.Logging
open Xunit
open FsUnit.Xunit
open Haumohio.Storage
open Haumohio.EventSourcing
open Haumohio.EventSourcing.EventStorage
open TestCommon


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
  cToday.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/TestProjection/2006-06-04.json"]

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
    "test1/TestProjection/2006-05-26.json"
    "test1/TestProjection/2006-05-27.json"
    "test1/TestProjection/2006-05-28.json"
    "test1/TestProjection/2006-05-29.json"
    "test1/TestProjection/2006-05-30.json"
    "test1/TestProjection/2006-05-31.json"
    "test1/TestProjection/2006-06-01.json"
    "test1/TestProjection/2006-06-02.json"
    "test1/TestProjection/2006-06-03.json"
    "test1/TestProjection/2006-06-04.json"
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
  let s05 = cToday.stateContainer.loadAs<TestState> "test1/TestProjection/2006-05-31.json"
  s05.Value.["42"].Value |> should equal {id="42"; cnt=2; stuff=[]}
  let s10 = cToday.stateContainer.loadAs<TestState> "test1/TestProjection/2006-05-26.json"
  s10.Value.["42"].Value |> should equal {id="42"; cnt=1; stuff=[]}
