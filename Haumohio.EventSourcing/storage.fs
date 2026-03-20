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

  let eventDate (filename: string) = 
    match filename.Split( [|'_'|], StringSplitOptions.RemoveEmptyEntries) with 
    | parts when parts.Length >= 3 ->
        parts[1] + " " + parts[2]
        |> DateTime.Parse
    | _ -> DateTime.MinValue

  type EventSourcingContainer<'D when 'D: enum<int>> = {
    events: StorageContainer
    projections: StorageContainer
  }with 
    member this.timeProvider = this.events.timeProvider
    member this.logger = this.events.logger
    member this.save = this.events.save
  
  let storeEvent<'E, 'D when 'E:> IHasDescription and 'D: enum<int>> (c:EventSourcingContainer<'D>) (domain:'D) userName (eventDetail:'E) =
    let event = { at = c.timeProvider(); by = userName; details = eventDetail }
    let dtString = event.at |> dateString
    let evtName = eventDetail |> DUName
    let partition = domain.ToString().ToLowerInvariant()
    let filename = $"{partition}/event_{dtString}_{evtName}"
    c.logger.LogDebug $"storing {filename}"
    c.save filename event
    :?> Event<'E>
    |> fun x -> { at = x.at; by = x.by; action = evtName; category=partition; description = eventDetail.description}

  let storeEvents<'E, 'D when 'E:> IHasDescription and 'D: enum<int>> (c:EventSourcingContainer<'D>) (domain:'D) userName (eventDetail:'E seq) =
    eventDetail
    |> Seq.map (storeEvent c domain userName)
    |> Seq.reduce ( fun acc i -> {acc with action = $"{acc.action}, {i.action}"; description = $"{acc.description},\r\n{i.description}" })
