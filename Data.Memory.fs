namespace Haumohio.EventSourcing
open System.Collections.Generic
open Haumohio.EventSourcing.Common

type MemoryEventSource() = 
  let events = new Dictionary<string, obj list>()

  interface EventSource with

    member this.load<'Tevt> domain =
      if events.ContainsKey domain |> not then events.[domain] <- []
      events.[domain]
      |> Seq.map ( fun x -> x :?> Timed<'Tevt>)

    member this.append domain eventsGenerated =
      if events.ContainsKey domain |> not then events.[domain] <- []
      eventsGenerated
      |> Seq.map box
      |> Seq.toList
      |> fun o -> events.[domain] <- events.[domain] @ o
      this :> _