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
  type DateProvider = unit -> DateTime
  type CommandProcessor<'S, 'C, 'E> = 'S -> 'C -> CommandResult<'E>
  type Projector<'S, 'E> = 'S -> 'E -> 'S

  let resolveMany (resolver: Projector<'S, 'E>) (state: 'S) (events: 'E seq) =
      events |> Seq.fold resolver state 

  let projectFromSource<'S, 'E> (initialState: 'S) projectFutureState  (src:EventSource) = 
    src.load<'E> "sportsball"
    |> resolveMany projectFutureState initialState

module Commands = 
  open Projection 

  let applyCommand<'S, 'C, 'E> 
      (utcNow: DateProvider)
      (logger: Logger)
      (processor: CommandProcessor<'S, 'C, 'E>)
      (initialState: 'S)
      (cmd: 'C) 
      : CommandResult<'E> =
    try
      processor initialState cmd
    with 
    | exc -> exc.ToString() |> Failure

  let applyCommandToEventSource<'S, 'C, 'E> 
      (utcNow: DateProvider)
      (logger: Logger)
      (processor: CommandProcessor<'S, 'C, 'E>)
      (initialState: 'S)
      (src:EventSource) 
      (cmd: 'C)=
    cmd 
    |> applyCommand utcNow logger processor initialState
    |> function 
        | Success result -> 
            result
            |> Seq.map (fun x -> {event=x; at=utcNow() })
            |> src.append "sportsball" 
        | Pass -> src
        | Failure msg -> msg |> System.Exception |> fail logger
