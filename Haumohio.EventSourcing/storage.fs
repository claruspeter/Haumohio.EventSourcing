namespace Haumohio.EventSourcing
open System

module EventStorage =
  open System.IO
  open Haumohio.Storage
  open Microsoft.Extensions.Logging

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

  type IEventResponse = 
    abstract member at: DateTime
    abstract member by: string
    abstract member domain: string
    abstract member action: string
    abstract member description: string    

  type EventStorageResponse = {
    at: DateTime
    by: string
    domain: string
    action: string
    description: string
  }with
    interface IEventResponse with
        member this.action: string = this.action
        member this.at: DateTime = this.at
        member this.by: string = this.by
        member this.description: string = this.description
        member this.domain: string = this.domain


  type IHasDescription = 
    abstract member description: string

  let dateOnlyString (dt:DateOnly) = 
    dt.ToString("yyyy-MM-dd")

  let dateString (dt:DateTime) = 
    dt.ToString("yyyy-MM-dd")

  let timeString (dt:DateTime) = 
    dt.ToString("HH-mm-ss.fff")

  let datetimeString dt =
    sprintf "%s_%s" (dateString dt) (timeString dt)

  let eventDate (filename: string) = 
    match filename.Split( [|'_'|], StringSplitOptions.RemoveEmptyEntries) with 
    | parts when parts.Length >= 3 ->
        parts[1] + " " + parts[2]
        |> DateTime.Parse
    | _ -> DateTime.MinValue

  type EventSourcingContainer = {
    eventContainer: StorageContainer
    stateContainer: StorageContainer
  }with 
    member this.timeProvider = this.eventContainer.timeProvider
    member this.logger = this.eventContainer.logger

  let storeEvent<'E, 'D when 'E:> IHasDescription and 'D: enum<int>> (c:EventSourcingContainer) (domain:'D) userName (eventDetail:'E) =
    let event = { at = c.timeProvider(); by = userName; details = eventDetail }
    let category = domain.ToString().ToLowerInvariant()
    let evtName = eventDetail |> DUName
    let filename = sprintf "%s/event/%s_%s" category (event.at |> datetimeString) evtName
    c.logger.LogDebug $"storing {filename}"
    c.eventContainer.save filename event
    :?> Event<'E>
    |> fun x -> { at = x.at; by = x.by; action = evtName; domain=category; description = eventDetail.description}

  let storeEvents<'E, 'D when 'E:> IHasDescription and 'D: enum<int>> (c:EventSourcingContainer) (domain:'D) userName (eventDetail:'E seq) =
    eventDetail
    |> Seq.map (storeEvent c domain userName)
    |> Seq.reduce ( fun acc i -> {acc with action = $"{acc.action}, {i.action}"; description = $"{acc.description},\r\n{i.description}" })

  let list (domain: 'D) (day:DateOnly) (container: EventSourcingContainer) =
    let prefix = sprintf "%s/event/%s" (domain.ToString().ToLowerInvariant()) (dateOnlyString day) 
    prefix
    |> container.eventContainer.list 
    |> Seq.map Path.GetFileName
    |> Seq.toList

