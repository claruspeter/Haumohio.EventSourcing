module TestCommon
open System
open System.Collections.Generic
open Haumohio.Extensions
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection
open Haumohio.EventSourcing.EventStorage

type TestEvents = 
  | Data of amount: int
  | Other of string
with
  interface IHasDescription with
    member this.description: string =
      match this with 
      | Data amt -> $"DATA:{amt}"
      | Other s -> $"OTHER:{s}"

type TestDomain = 
    | Test1 = 1
    | Flo = 2

let EventStore c = 
  {
    eventContainer=c
    stateContainer=c
  }

type TestProjection = {
  id: string
  cnt: int
  stuff: int list
}with
  interface IHasKey<string> with 
    member this.Key = this.id
  interface IEmpty<TestProjection> with 
    static member empty = {id=""; cnt=0; stuff = []}
  interface IAutoClean<TestProjection> with 
    member this.clean() = 
      {this with 
        stuff = if this.stuff = [] then [123] else this.stuff
      }

type TestState = Projection.State<string, TestProjection>
let stateVersion = 1

let projector (state: TestState) (ev: Event<TestEvents>) =
  match ev.details with 
  | Data x -> addOrAmend (x.ToString()) (fun p -> {p with id=x.ToString(); cnt=p.cnt + 1}) state
  | _ -> state // do nothing 

let empty = State<string, TestProjection>.empty

let mutable _test_now = new DateTime(2026, 1,2,3,4,5,6, DateTimeKind.Utc)
let incrementingTimeProvider = fun () -> 
  _test_now <- _test_now.AddSeconds(1)
  _test_now