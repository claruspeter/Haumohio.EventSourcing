namespace Haumohio.EventSourcing
open System

module EventStorage =
  open Haumohio.Storage

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

  type EventStorageResponse = {
    at: DateTime
    by: string
    action: string
    description: string
  }

  type IHasDescription = 
    abstract member description: string
  

  let storeEvent<'Tevent when 'Tevent:> IHasDescription> (c:StorageContainer) userName (eventDetail:'Tevent) =
    let event = { at = DateTime.UtcNow; by = userName; details = eventDetail }
    c.save $"event_{event.at: u}" event
    :?> Event<'Tevent>
    |> fun x -> { at = x.at; by = x.by; action = eventDetail |> DUName; description = eventDetail.description}

