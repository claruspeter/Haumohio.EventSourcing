namespace Haumohio.EventSourcing
open System
open System.Linq
open System.Collections.Generic
open Microsoft.Extensions.Logging

module StateStorage =
  open Haumohio.Storage
  open Haumohio
  open EventStorage
  open Projection

  let findInitialEventDate (domain: 'D) (container: EventSourcingContainer) =
    let folder = sprintf "%s/event/" (domain.ToString().ToLowerInvariant())
    folder
    |> container.eventContainer.list
    |> Seq.tryHead
    |> Option.map (fun x -> x.Substring(folder.Length, 10) |> DateOnly.Parse )

  let loadStateForDay (domain: 'D) (day:DateOnly) (container: EventSourcingContainer) =
      let filename = sprintf "%s/%s/%s" (domain.ToString().ToLowerInvariant()) (typeof<'S>.Name) (day |> dateOnlyString)
      container.stateContainer.loadAs<State<'K, 'S>> filename

  let rec makeStateForDay (empty: State<'K,'S>) (domain: 'D) (day:DateOnly) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer) =
    container.logger.LogDebug("Making state {Domain}.{StateName} for {Today}", domain, typeof<'S>.Name, day)
    match loadStateForDay domain day container with 
    | Some state ->
      container.logger.LogDebug("Using previously calculated state")
      state
    | None ->
      let initial = 
        match findInitialEventDate domain container with 
        | Some x when x < day -> 
          container.logger.LogDebug("No state for previous day {Day} - projecting...", (day.AddDays -1))
          makeStateForDay empty domain (day.AddDays -1) projector container
        | Some _ ->
          container.logger.LogDebug("Initial event is {Day} - start from empty", day)
          empty
        | None ->
          container.logger.LogDebug("No initial event - start from empty")
          empty
      let state = projectForDay projector domain day initial container
      let filename = sprintf "%s/%s/%s" (domain.ToString().ToLowerInvariant()) (typeof<'S>.Name) (day |> dateOnlyString)
      let saved = container.stateContainer.save filename state
      container.logger.LogInformation("State {Domain}.{StateName} for {Day} saved", domain, (typeof<'S>.Name), day)
      state

  let makeStateWithEmpty (empty: State<'K,'S>) (domain: 'D) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer) =
    let today = container.stateContainer.timeProvider() |> DateOnly.FromDateTime 
    container.logger.LogDebug("Making state {Domain}.{StateName} for today {Today}", domain, typeof<'S>.Name, today)
    let firstEvent = findInitialEventDate domain container
    container.logger.LogDebug("Initial Event Date in {Domain}: {InitialDate}", domain, firstEvent)
    let initial = 
      match firstEvent with 
      | Some x when x < today -> 
        let yesterday = today |> _.AddDays(-1)
        match loadStateForDay domain yesterday container with 
        | Some state -> 
          container.logger.LogDebug("Starting from yesterday's state")
          state
        | None -> 
          container.logger.LogDebug("No state for yesterday - projecting...")
          makeStateForDay empty domain yesterday projector container
      | Some _ -> 
        container.logger.LogDebug("Initial event is today - start from empty")
        empty
      | None -> 
        container.logger.LogDebug("No initial event - start from empty")
        empty
    projectForDay projector domain today initial container

  let makeState (domain: 'D) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer) =
    makeStateWithEmpty State<'K, 'S>.empty domain projector container