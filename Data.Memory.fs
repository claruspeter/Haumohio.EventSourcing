namespace Haumohio.EventSourcing
open System.Collections.Generic
open Haumohio.EventSourcing.Common

type MemoryEventSource(domain) = 
  inherit  EventSource(domain)

  let events = new Dictionary<string, obj list>()

  override this.loadFromDomain<'Tevt> domain =
    if events.ContainsKey domain |> not then events.[domain] <- []
    events.[domain]
    |> Seq.map ( fun x -> x :?> Timed<'Tevt>)

  override this.appendToDomain domain eventsGenerated =
    if events.ContainsKey domain |> not then events.[domain] <- []
    eventsGenerated
    |> Seq.map box
    |> Seq.toList
    |> fun o -> events.[domain] <- events.[domain] @ o
    this :> _