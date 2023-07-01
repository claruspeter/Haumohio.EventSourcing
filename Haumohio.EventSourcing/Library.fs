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

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

  let internal DUValue (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, zz -> zz

  type EventStorageResponse = {
    at: DateTime
    by: string
    action: string
  }

  let storeEvent<'Tevent> (c:StorageContainer) userName (eventDetail:'Tevent) =
    let event = { at = DateTime.UtcNow; by = userName; details = eventDetail }
    c.save $"event_{event.at: u}" event
    :?> Event<'Tevent>
    |> fun x -> { at = x.at; by = x.by; action = eventDetail |> DUName}

