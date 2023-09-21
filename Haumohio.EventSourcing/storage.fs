namespace Haumohio.EventSourcing
open System

module EventStorage =
  open Haumohio.Storage
  open Microsoft.Extensions.Logging

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

  let dateString (dt:DateTime) = 
    dt.ToString("yyyy-MM-dd_HH-mm-ss.fff")
  

  let storeEvent<'Tevent when 'Tevent:> IHasDescription> (c:StorageContainer) partition userName (eventDetail:'Tevent) =
    let event = { at = DateTime.UtcNow; by = userName; details = eventDetail }
    let dtString = event.at |> dateString
    let evtName = eventDetail |> DUName
    let filename = $"{partition}/event_{dtString}_{evtName}"
    c.logger.LogDebug $"storing {filename}"
    c.save filename event
    :?> Event<'Tevent>
    |> fun x -> { at = x.at; by = x.by; action = evtName; category=partition; description = eventDetail.description}

  let storeEvents<'Tevent when 'Tevent:> IHasDescription> (c:StorageContainer) partition userName (eventDetail:'Tevent seq) =
    eventDetail
    |> Seq.map (storeEvent c partition userName)
    |> Seq.reduce ( fun acc i -> {acc with action = $"{acc.action}, {i.action}"; description = $"{acc.description},\r\n{i.description}" })
