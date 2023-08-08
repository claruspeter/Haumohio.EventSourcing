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
    category: string
    action: string
    description: string
  }

  type IHasDescription = 
    abstract member description: string
  

  let storeEvent<'Tevent when 'Tevent:> IHasDescription> (c:StorageContainer) partition userName (eventDetail:'Tevent) =
    let event = { at = DateTime.UtcNow; by = userName; details = eventDetail }
    let dtString = event.at.ToString("u").Replace(':', '-').Replace(' ', '_')
    let evtName = eventDetail |> DUName
    let filename = $"{partition}/event_{dtString}_{evtName}"
    printfn $"storing {filename}"
    c.save filename event
    :?> Event<'Tevent>
    |> fun x -> { at = x.at; by = x.by; action = evtName; category=partition; description = eventDetail.description}

