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
open Haumohio

type MyLogger() =
  let mutable _logs : string list = []

  member this.Logs = _logs

  interface ILogger with
      member this.BeginScope(state: 'TState): IDisposable = 
          this
      member this.IsEnabled(logLevel: LogLevel): bool = 
          true
      member this.Log(logLevel: LogLevel, eventId: EventId, state: 'TState, ``exception``: exn, formatter: Func<'TState,exn,string>): unit = 
          let s = formatter.Invoke (state, ``exception``) |> sprintf "%O: %s" logLevel
          _logs <- _logs @ [s]
  interface IDisposable with
      member this.Dispose(): unit = 
          ()


let _now = fun () -> DateTime(2006, 6,5,4,3,2,1)
let _today = _now() |> DateOnly.FromDateTime

let _logger = new MyLogger()  //LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore).CreateLogger("EventStorageTests")
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
  let _ = StateStorage.makeState TestDomain.Test1 projector stateVersion cToday
  //Assert
  cToday.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/TestProjection_v1/2006-06-04"]


[<Fact>]
let ``State is stored in the state container in a versioned folder`` () = 
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
  let _ = StateStorage.makeState TestDomain.Test1 projector 2 cToday
  //Assert
  cToday.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/TestProjection_v2/2006-06-04"]


[<Fact>]
let ``Incrementing the state version cause the state to be rebuilt`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let cYesterday = setTime container (_now().AddDays(-1))
  let _ = storeEvent cYesterday TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime cYesterday (_now())
  //Act 1
  let _ = StateStorage.makeState TestDomain.Test1 projector 1 cToday
  //Assert 1
  cToday.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/TestProjection_v1/2006-06-04"]
  //Act 2
  let _ = StateStorage.makeState TestDomain.Test1 projector 2 cToday
  //Assert 2
  cToday.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/TestProjection_v1/2006-06-04"; "test1/TestProjection_v2/2006-06-04"]

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
  let state = StateStorage.makeState TestDomain.Test1 projector stateVersion container
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
  let _ = StateStorage.makeState TestDomain.Test1 projector stateVersion cToday
  //Assert
  cToday.stateContainer.list "test1" 
  |> Seq.toList 
  |> should equal [
    "test1/TestProjection_v1/2006-05-26"
    "test1/TestProjection_v1/2006-05-27"
    "test1/TestProjection_v1/2006-05-28"
    "test1/TestProjection_v1/2006-05-29"
    "test1/TestProjection_v1/2006-05-30"
    "test1/TestProjection_v1/2006-05-31"
    "test1/TestProjection_v1/2006-06-01"
    "test1/TestProjection_v1/2006-06-02"
    "test1/TestProjection_v1/2006-06-03"
    "test1/TestProjection_v1/2006-06-04"
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
  let _ = StateStorage.makeState TestDomain.Test1 projector stateVersion cToday
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
  let state = StateStorage.makeStateWithEmpty initial TestDomain.Test1 projector stateVersion container
  //Assert
  container.stateContainer.list "test1" |> Seq.length |> should equal 0
  state.["42"].IsSome |> should equal true
  let result = state.["42"].Value
  result.cnt |> should equal 11
  result.stuff |> should equal [1;2;3]

[<Fact>]
let ``State can re reloaded`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let c2 = setTime container (_now().AddDays(-2))
  let _ = storeEvent c2 TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime c2 (_now())
  let _ = StateStorage.makeState TestDomain.Test1 projector stateVersion cToday  //builds the states
  //Act
  let state: State<string, TestProjection> option = StateStorage.loadStateForDay TestDomain.Test1 (_today.AddDays(-1)) stateVersion container
  //Assert
  state.IsSome |> should equal true
  state.Value.["42"].IsSome |> should equal true
  state.Value.["42"].Value.id |> should equal "42"

[<Fact>]
let ``State is cleaned`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let c2 = setTime container (_now().AddDays(-2))
  let _ = storeEvent c2 TestDomain.Test1 "test_user" (Data 42)
  let cToday = setTime c2 (_now())
  let _ = StateStorage.makeState TestDomain.Test1 projector stateVersion cToday  //builds the states
  //Act
  let state: State<string, TestProjection> option = StateStorage.loadStateForDay TestDomain.Test1 (_today.AddDays(-1)) stateVersion container
  //Assert
  state.IsSome |> should equal true
  state.Value.["42"].IsSome |> should equal true
  state.Value.["42"].Value.stuff |> should equal [123]
