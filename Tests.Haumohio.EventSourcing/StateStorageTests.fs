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
let ``State is stored in it's own container`` () = 
  //Arrange
  let store = newStore()
  let container = {
    eventContainer = store.container "events"
    stateContainer = store.container "states"
  }
  let _ = storeEvent container TestDomain.Test1 "test_user" (Data 42)
  //Act
  let _ = StateStorage.makeState TestDomain.Test1 projector container
  //Assert
  container.stateContainer.list "test1" |> Seq.toList |> should equal ["test1/20060605/TestProjection.json"]