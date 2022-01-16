module Tests

open System
open Xunit
open FsUnit.Xunit
open Haumohio.EventSourcing
open Domain

let USER = "TESTS"
let SRC() = MemoryEventSource<Event>("people")


[<Fact>]
let ``Can create a person`` () =
  let src = SRC()
  let created = 
    "Fred"
    |> AddPerson
    |> processPeopleCommand src USER
    |> projectPeople
    |> fun s -> s.people.Head
  created.name |> should equal "Fred"

[<Fact>]
let ``Can update a person`` () =
  let src = SRC()
  let created = 
    AddPerson "Fred"
    |> processPeopleCommand src USER
  let firstProjection = created |> projectPeople |> fun s -> s.people.Head
  let updated =
    UpdatePersonDetails {person=firstProjection.id; name=None; phone=None; email=Some "me@here.com"}
    |> processPeopleCommand created USER
  let projection = 
    updated
    |> projectPeople
    |> fun s -> s.people.Head
  projection.name |> should equal "Fred"
  projection.email |> should equal "me@here.com"