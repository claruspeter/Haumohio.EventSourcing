module EventStorageTests
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

[<Fact>]
let ``Event is stored under DOMAIN/DAY/event`` () =
  //Arrange
  let store = newStore()
  let container = store.container "TEST" |> EventStore
  //Act
  let response = storeEvent container TestDomain.Test1 "test_user" (Data 42)
  //Assert
  container.eventContainer.list "" |> Seq.toList |> should equal ["test1/20060605/event_04-03-02.001_Data.json"]
  container.eventContainer.part "" |> Seq.toList |> should equal ["test1"]
  container.eventContainer.part "test1/" |> Seq.toList |> should equal ["20060605"]
  container.eventContainer.list "test1/20060605" |> Seq.toList |> should equal ["test1/20060605/event_04-03-02.001_Data.json"]

[<Fact>]
let ``Events can be retrieved by day for a domain`` () =
  //Arrange
  let store = newStore()
  let container = store.container "TEST" |> EventStore
  let yesterday = setTime container (_now().AddDays(-1).AddHours(-1)) 
  let _ = storeEvent yesterday TestDomain.Test1 "test_user" (Data 41)  // yesterday's event not retrieved
  let c1 = setTime yesterday (_now())
  let _ =  storeEvent c1 TestDomain.Test1 "test_user" (Data 42)
  let c2 = setTime c1 (_now().AddHours(1))
  let _ =  storeEvent c2 TestDomain.Test1 "test_user" (Data 43)
  let c3 = setTime c2 (_now().AddHours(2))
  let _ =  storeEvent c3 TestDomain.Flo "test_user" (Data 43)  // event for another domain not retrieved
  //Act
  let events = EventStorage.list TestDomain.Test1 _today c3
  //Assert
  events |> should equal ["event_04-03-02.001_Data"; "event_05-03-02.001_Data"]
