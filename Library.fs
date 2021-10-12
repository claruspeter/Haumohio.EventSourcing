namespace Haumohio.EventSourcing
open System
open Haumohio.EventSourcing.Common

type Timed<'E> = {
  event: 'E
  at: DateTime
}

type Logger = string -> unit

type CommandResult<'E> =
  | Success of 'E seq
  | Failure of string
  | Pass
with 
  member this.append (res2:CommandResult<'E>) =
    match this, res2 with 
    | Failure _, _ -> this
    | _ , Failure _ -> res2
    | Pass, _ -> res2
    | _, Pass -> this
    | Success x, Success y -> Seq.append x y |> Success

type EventSource = 
  abstract member load<'E> : string -> Timed<'E> seq
  abstract member append<'E> : string -> Timed<'E> seq -> EventSource


module Projection =
  type Projector<'S, 'E> = 'S -> Timed<'E> -> 'S

  let resolveMany (resolver: Projector<'S, 'E>) (state: 'S) (events: Timed<'E> seq) =
      events |> Seq.fold resolver state 

  let projectFromSource<'S, 'E> (initialState: 'S) (projectFutureState: Projector<'S, 'E>)  (src:EventSource) = 
    src.load<'E> "sportsball"
    |> resolveMany projectFutureState initialState

type CommandProcessor<'S, 'C, 'E> = 'S -> 'C -> CommandResult<'E>
type DateProvider = unit -> DateTime

open Projection 
type private BatchResult<'S, 'E> = {
  prev: CommandResult<'E>
  state: 'S
}

type Commands<'S, 'C, 'E> = {
  utcNow: DateProvider
  logger: Logger
  processor: CommandProcessor<'S, 'C, 'E>
}
with 
  member this.applyCommand
      (initialState: 'S)
      (cmd: 'C) 
      : CommandResult<'E> =
    try
      this.processor initialState cmd
    with 
    | exc -> 
      exc.ToString() |> this.logger
      exc.Message |> Failure

  member this.applyResultToEventSource 
      (src: EventSource) 
      (result:CommandResult<'E>) =
    match result with 
    | Success result -> 
        result
        |> Seq.map (fun x -> {event=x; at=this.utcNow() })
        |> src.append "sportsball" 
    | Pass -> src
    | Failure msg -> msg |> System.Exception |> fail this.logger

  member this.applyCommandToEventSource
      (initialState: 'S)
      (src:EventSource) 
      (cmd: 'C)=
    cmd 
    |> this.applyCommand initialState
    |> this.applyResultToEventSource src

  member this.applyBatchToEventSource
      (projector: Projector<'S, 'E>)
      (initialState: 'S)
      (src:EventSource) 
      (batch: ('S -> CommandResult<'E>) seq) =
    
    batch 
    |> Seq.fold (
        fun acc cmd ->
          let res = cmd acc.state
          match res with 
          | Failure msg -> { prev=Failure msg; state=acc.state } 
          | Pass -> { prev=Pass; state=acc.state } 
          | Success events -> { prev=acc.prev.append res; state=events |> Seq.map (fun e -> {event=e; at=this.utcNow()}) |> resolveMany projector acc.state  }

      ) 
      { prev=CommandResult<'E>.Pass; state=initialState }
    |> fun x -> x.prev
    |> this.applyResultToEventSource src