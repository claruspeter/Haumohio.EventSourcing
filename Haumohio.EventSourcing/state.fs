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

  let findInitialEventDate (domain: 'D) (container: EventSourcingContainer<'D>) =
    let folder = sprintf "%s/event/" (domain.ToString().ToLowerInvariant())
    folder
    |> container.eventContainer.list
    |> Seq.tryHead
    |> Option.map (fun x -> x.Substring(folder.Length, 10) |> DateOnly.Parse )

  let loadStateForDay (domain: 'D) (day:DateOnly) (container: EventSourcingContainer<'D>) =
      let filename = sprintf "%s/%s/%s.json" (domain.ToString().ToLowerInvariant()) (typeof<'S>.Name) (day |> dateOnlyString)
      container.stateContainer.loadAs<State<'K, 'S>> filename

  let rec makeStateForDay (domain: 'D) (day:DateOnly) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer<'D>) =
    match loadStateForDay domain (day.AddDays -1) container with 
    | Some state -> state
    | None ->
      let initial = 
        match findInitialEventDate domain container with 
        | Some x when x < day -> makeStateForDay domain (day.AddDays -1) projector container
        | _ -> State<'K, 'S>.empty
      let state = projectForDay projector domain day initial container
      let filename = sprintf "%s/%s/%s.json" (domain.ToString().ToLowerInvariant()) (typeof<'S>.Name) (day |> dateOnlyString)
      let saved = container.stateContainer.save filename state
      state

  let makeState (domain: 'D) (projector: Projector<'K, 'S, 'E>) (container: EventSourcingContainer<'D>) =
    let today = container.stateContainer.timeProvider() |> DateOnly.FromDateTime 
    let firstEvent = findInitialEventDate domain container
    let initial = 
      match firstEvent with 
      | Some x when x < today -> 
        let yesterday = today |> _.AddDays(-1)
        match loadStateForDay domain yesterday container with 
        | Some state -> state
        | None -> makeStateForDay domain yesterday projector container
      | _ -> State<'K, 'S>.empty
    projectForDay projector domain today initial container
