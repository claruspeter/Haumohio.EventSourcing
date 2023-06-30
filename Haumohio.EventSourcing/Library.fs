namespace Haumohio.EventSourcing
open System

type UserId = string

[<CLIMutable>]
type Event<'T> = {
  at: DateTime
  by: UserId
  details: 'T
}

module EventStorage =
  open Haumohio.Storage

  let storeEvent<'Tevent> (c:StorageContainer) userName (eventDetail:'Tevent) : Event<'Tevent> =
    let event = { at = DateTime.UtcNow; by = userName; details = eventDetail }
    c.save $"event_{event.at: u}" event :?> _
