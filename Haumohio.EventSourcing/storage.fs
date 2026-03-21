namespace Haumohio.EventSourcing
open System

module EventStorage =
  open System.IO
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
    dt.ToString("yyyyMMdd")

  let timeString (dt:DateTime) = 
    dt.ToString("HH-mm-ss.fff")

  let eventDate (filename: string) = 
    match filename.Split( [|'_'|], StringSplitOptions.RemoveEmptyEntries) with 
    | parts when parts.Length >= 3 ->
        parts[1] + " " + parts[2]
        |> DateTime.Parse
    | _ -> DateTime.MinValue

  type EventSourcingContainer<'D when 'D: enum<int>> = {
    eventContainer: StorageContainer
    stateContainer: StorageContainer
  }with 
    member this.timeProvider = this.eventContainer.timeProvider
    member this.logger = this.eventContainer.logger

  let storeEvent<'E, 'D when 'E:> IHasDescription and 'D: enum<int>> (c:EventSourcingContainer<'D>) (domain:'D) userName (eventDetail:'E) =
    let event = { at = c.timeProvider(); by = userName; details = eventDetail }
    let category = domain.ToString().ToLowerInvariant()
    let evtName = eventDetail |> DUName
    let partition = sprintf "%s/%s" category (event.at |> dateString)
    let filename = sprintf "%s/event_%s_%s.json" partition (event.at |> timeString) evtName
    c.logger.LogWarning $"storing {filename}"
    c.eventContainer.save filename event
    :?> Event<'E>
    |> fun x -> { at = x.at; by = x.by; action = evtName; category=category; description = eventDetail.description}

  let storeEvents<'E, 'D when 'E:> IHasDescription and 'D: enum<int>> (c:EventSourcingContainer<'D>) (domain:'D) userName (eventDetail:'E seq) =
    eventDetail
    |> Seq.map (storeEvent c domain userName)
    |> Seq.reduce ( fun acc i -> {acc with action = $"{acc.action}, {i.action}"; description = $"{acc.description},\r\n{i.description}" })

  let list (domain: 'D) (day:DateOnly) (container: EventSourcingContainer<'D>) =
    let folder = sprintf "%s/%s/" (domain.ToString().ToLowerInvariant()) (day.ToString "yyyyMMdd") 
    folder
    |> container.eventContainer.list 
    |> Seq.map Path.GetFileNameWithoutExtension
    |> Seq.toList