namespace Haumohio.EventSourcing
open System
open Microsoft.Extensions.Logging

type EventStore<'TEvent> = {
  /// <summary>A provider of the date & time</summary>
  timeProvider: unit -> DateTime
  /// <summary>A logger of messages</summary>
  logger: ILogger
  /// <summary>Save item by full name</summary> 
  save: string -> Event<'TEvent> -> Event<'TEvent>
  /// <summary>List of items with prefix</summary>
  list: string -> string seq
  /// <summary>Load item by full name</summary>
  load: string -> (string -> bool) -> Event<'TEvent> seq  
} 

module EventStorage =

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
  
  let storeEvent<'TEvent when 'TEvent:> IHasDescription> (c:EventStore<'TEvent>) partition userName (eventDetail:'TEvent) =
    let event = { at = c.timeProvider(); by = userName; details = eventDetail }
    let dtString = event.at |> dateString
    let evtName = eventDetail |> DUName
    let filename = $"{partition}/event_{dtString}_{evtName}"
    c.logger.LogDebug $"storing {filename}"
    c.save filename event
    |> fun x -> { at = x.at; by = x.by; action = evtName; category=partition; description = eventDetail.description}

  let storeEvents<'TEvent when 'TEvent:> IHasDescription> (c:EventStore<'TEvent>) partition userName (eventDetail:'TEvent seq) =
    eventDetail
    |> Seq.map (storeEvent c partition userName)
    |> Seq.reduce ( fun acc i -> {acc with action = $"{acc.action}, {i.action}"; description = $"{acc.description},\r\n{i.description}" })

  let since<'TEvent> (store: EventStore<'TEvent>) partition (after: DateTime) =
    let dtString = after |> dateString
    let limit = $"event_{dtString}"
    store.load
      partition
      (fun x -> 
        let fn = x.Split '/' |> Array.last
        if fn.StartsWith "event" then
          fn >= limit
        else
          false
      )
