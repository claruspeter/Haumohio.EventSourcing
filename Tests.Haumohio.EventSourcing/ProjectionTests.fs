module ProjectionTests

open System
open Microsoft.Extensions.Logging
open Xunit
open FsUnit.Xunit
open Haumohio.Storage
open Haumohio.EventSourcing
open Haumohio.EventSourcing.EventStorage
open Haumohio.EventSourcing.Projection
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
let ``Event can be projected`` () = 
  //Arrange
  let store = newStore()
  let container = store.container "TEST" |> EventStore
  let _ = storeEvent container TestDomain.Test1 "test_user" (Data 42)
  //Act
  let projection = project projector TestDomain.Test1 TestState.empty container
  //Assert
  projection.data.Count |> should equal 1
  projection.["42"] |> should equal (Some {id="42"; cnt=1; stuff=[]})


[<Fact>]
let ``Event can be projected for a certain day`` () = 
  //Arrange
  let store = newStore()
  let container = store.container "TEST" |> EventStore
  let c1 = setTime container (_now().AddDays(-2))
  let _ = storeEvent c1 TestDomain.Test1 "test_user" (Data 1)
  let c2 = setTime c1 (_now().AddDays(-1))
  let _ = storeEvent c2 TestDomain.Test1 "test_user" (Data 2)
  let c3 = setTime c2 (_now().AddDays(0))
  let _ = storeEvent c3 TestDomain.Test1 "test_user" (Data 3)
  //Act
  let projection = projectForDay projector TestDomain.Test1 (_now().AddDays(-1) |> DateOnly.FromDateTime) TestState.empty container
  //Assert
  projection.data.Count |> should equal 1
  projection.["1"] |> should equal None
  projection.["2"] |> should equal (Some {id="2"; cnt=1; stuff=[]})
  projection.["3"] |> should equal None