namespace Haumohio.EventSourcing
open System

type UserId = string

type Event<'T> = {
  at: DateTime
  by: UserId
  details: 'T
}
