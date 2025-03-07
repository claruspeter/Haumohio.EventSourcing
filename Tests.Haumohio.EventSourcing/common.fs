module TestCommon
open System
open System.Collections.Generic
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

type TestProjection = {
  id: string
  sum: int
  stuff: int list
}with
  interface IHasKey<string> with 
    member this.Key = this.id
  interface IEmpty<TestProjection> with 
    static member empty = {id=""; sum=0; stuff = []}
  interface IAutoClean<TestProjection> with 
    member this.clean() = 
      {this with 
        stuff = unNull [] this.stuff
      }

type TestState = Projection.State<string, TestProjection>

let projector (state: TestState) (ev: Event<TestEvents>) =
  match ev.details with 
  | Data x -> 
    state.data.Add(KeyValuePair(x.ToString(), {id=x.ToString(); sum=x; stuff=[]}))
    state
  | _ -> state // do nothing 

let empty = State<string, TestProjection>.empty 1
