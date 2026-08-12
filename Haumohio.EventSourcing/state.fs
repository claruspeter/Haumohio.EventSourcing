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

  let rec snakedTypeName (tp: Type) =
    match tp with 
    | t when t.IsGenericTypeDefinition -> t.Name.Remove(t.Name.IndexOf('`'))
    | t when t.IsGenericType -> 
      let names = 
        [t.GetGenericTypeDefinition() |> snakedTypeName]
        @ (
          t.GetGenericArguments() 
          |> Seq.map snakedTypeName
          |> Seq.toList
        )
      String.Join('-', names)
    | t -> t.Name

  let simpleTypeName<'X> = typeof<'X> |> snakedTypeName

  let findInitialEventDate (domain: 'D) (container: EventSourcingContainer) =
    let folder = sprintf "%s/event/" (domain.ToString().ToLowerInvariant())
    folder
    |> container.eventContainer.list
    |> Seq.tryHead
    |> Option.map (fun x -> x.Substring(folder.Length, 10) |> DateOnly.Parse )

  let loadStateForDay (domain: 'D) (day:DateOnly) (version: int) (container: EventSourcingContainer) =
      let filename = sprintf "%s/%s_v%d/%s" (domain.ToString().ToLowerInvariant()) simpleTypeName<'S> version (day |> dateOnlyString)
      container.stateContainer.loadAs<State<'K, 'S>> filename
      |> Option.map autoClean

  let rec makeStateForDay (empty: State<'K,'S>) (domain: 'D) (day:DateOnly) (projector: Projector<'K, 'S, 'E>) (version: int) (container: EventSourcingContainer) =
    container.logger.LogDebug("Making state {Domain}.{StateName} v{Version} for {Today}", domain, simpleTypeName<'S>, version, day)
    match loadStateForDay domain day version container with 
    | Some state ->
      container.logger.LogDebug("Using previously calculated state")
      state
    | None ->
      let initial = 
        match findInitialEventDate domain container with 
        | Some x when x < day -> 
          container.logger.LogDebug("No state for previous day {Day} - projecting...", (day.AddDays -1))
          makeStateForDay empty domain (day.AddDays -1) projector version container
        | Some _ ->
          container.logger.LogDebug("Initial event is {Day} - start from empty", day)
          empty
        | None ->
          container.logger.LogDebug("No initial event - start from empty")
          empty
      let state = projectForDay projector domain day initial container
      let filename = sprintf "%s/%s_v%d/%s" (domain.ToString().ToLowerInvariant()) simpleTypeName<'S> version (day |> dateOnlyString)
      let saved = container.stateContainer.save filename {state with version = version}
      container.logger.LogInformation("State {Domain}.{StateName} v{Version} for {Day} saved", domain, simpleTypeName<'S>, version, day)
      state

  let makeStateWithEmpty (empty: State<'K,'S>) (domain: 'D) (projector: Projector<'K, 'S, 'E>) (version: int) (container: EventSourcingContainer) =
    let today = container.stateContainer.timeProvider() |> DateOnly.FromDateTime 
    container.logger.LogDebug("Making state {Domain}.{StateName} v{Version} for today {Today}", domain, simpleTypeName<'S>, version, today)
    let firstEvent = findInitialEventDate domain container
    container.logger.LogDebug("Initial Event Date in {Domain}: {InitialDate}", domain, firstEvent)
    let initial = 
      match firstEvent with 
      | Some x when x < today -> 
        let yesterday = today |> _.AddDays(-1)
        match loadStateForDay domain yesterday version container with 
        | Some state -> 
          container.logger.LogDebug("Starting from yesterday's state")
          state
        | None -> 
          container.logger.LogDebug("No state for yesterday - projecting...")
          makeStateForDay empty domain yesterday projector version container
      | Some _ -> 
        container.logger.LogDebug("Initial event is today - start from empty")
        empty
      | None -> 
        container.logger.LogDebug("No initial event - start from empty")
        empty
    projectForDay projector domain today initial container

  let makeState (domain: 'D) (projector: Projector<'K, 'S, 'E>) (version: int) (container: EventSourcingContainer) =
    makeStateWithEmpty State<'K, 'S>.empty domain projector version container