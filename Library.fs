namespace Haumohio.EventSourcing
open System
open Haumohio.EventSourcing.Common

type Timed<'E> = {
  event: 'E
  at: DateTime
  by: UserId
}

type UserEvents<'E> = {
  by: UserId
  events: 'E seq
}


type CommandResult<'E> =
  | Success of UserEvents<'E>
  | Failure of UserId * string
  | Pass
with 
  member this.append (res2:CommandResult<'E>) =
    match this, res2 with 
    | Failure _, _ -> this
    | _ , Failure _ -> res2
    | Pass, _ -> res2
    | _, Pass -> this
    | Success x, Success y -> { by=x.by; events=Seq.append x.events y.events } |> Success

type EventSource = 
  abstract member load<'E> : string -> Timed<'E> seq
  abstract member append<'E> : string -> Timed<'E> seq -> EventSource


module Projection =
  type Projector<'S, 'E> = 'S -> Timed<'E> -> 'S

  let resolveMany (resolver: Projector<'S, 'E>) (state: 'S) (events: Timed<'E> seq) =
      events |> Seq.fold resolver state 

  let projectFromSource<'S, 'E> (initialState: 'S) (projectFutureState: Projector<'S, 'E>)  (src:EventSource) (domain: string)= 
    src.load<'E> domain
    |> resolveMany projectFutureState initialState

type CommandProcessor<'S, 'C, 'E> = 'S -> UserId -> 'C -> CommandResult<'E>
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
      (user: UserId)
      (cmd: 'C) 
      : CommandResult<'E> =
    try
      this.processor initialState user cmd
    with 
    | exc -> 
      exc.ToString() |> this.logger user
      (user, exc.Message) |> Failure

  member this.applyResultToEventSource 
      (src: EventSource) 
      (domain: string)
      (result:CommandResult<'E>) =
    match result with 
    | Success result -> 
        result.events
        |> Seq.map (fun x -> {event=x; at=this.utcNow(); by=result.by })
        |> src.append domain 
    | Pass -> src
    | Failure (user, msg) -> msg |> System.Exception |> fail this.logger user

  member this.applyCommandToEventSource
      (initialState: 'S)
      (src:EventSource) 
      (domain: string)
      (user: UserId)
      (cmd: 'C)=
    cmd 
    |> this.applyCommand initialState user
    |> this.applyResultToEventSource src domain

  member this.applyBatchToEventSource
      (projector: Projector<'S, 'E>)
      (initialState: 'S)
      (src:EventSource) 
      (domain: string)
      (batch: ('S -> CommandResult<'E>) seq) =
    
    batch 
    |> Seq.fold (
        fun acc cmd ->
          let res = cmd acc.state
          match res with 
          | Failure (user, msg) -> { prev=Failure (user, msg); state=acc.state } 
          | Pass -> { prev=Pass; state=acc.state } 
          | Success x -> 
              { 
                prev=acc.prev.append res
                state=
                  x.events 
                  |> Seq.map (fun e -> {event=e; at=this.utcNow(); by=x.by}) 
                  |> resolveMany projector acc.state  
              }

      ) 
      { prev=CommandResult<'E>.Pass; state=initialState }
    |> fun x -> x.prev
    |> this.applyResultToEventSource src domain