namespace Haumohio.EventSourcing
open System

type UserId = string

[<CLIMutable>]
type Event<'T> = {
  at: DateTime
  by: UserId
  details: 'T
}
