module Domain
open System
open Haumohio.EventSourcing
open Haumohio.EventSourcing.Projection

let mutable UtcNow = fun() -> DateTime.UtcNow
let log = fun user msg -> printfn "%s:%s: %s" (UtcNow().ToString("u")) user msg

/// <summary>A connected item with a name and id</summary>
type Named = {
  /// <summary>The unique identifier </summary>
  id: Guid
   /// <summary>The name </summary>
  name: string
}with 
  override this.ToString() = sprintf "%s (%A)" this.name this.id
  static member create name = {id=Guid.NewGuid(); name=name}


/// <summary>A person in the world</summary>
type Person = {
  /// <summary>The unique identifier of the person</summary>
  id: Guid
  /// <summary>The name of the person</summary>
  name: string
  /// <summary>A contact email address</summary>
  email: string
  /// <summary>A contact phone number</summary>
  phone: string
}with
  static member empty = {id=Guid.Empty; name="no-one"; email=""; phone=""}
  member this.asNamed = {Named.id=this.id; name=this.name}

type State = {
  people: Person list
}with 
  static member initial = {people=[]}
  member this.person = fun id -> this.people |> Seq.tryFind( fun x -> x.id = id)  

type Event = 
  | PersonCreated of Named
  | PersonUpdated of {| person: Named; name:string option; email: string option; phone: string option |}

let private createPerson state (x:Named) =
  {state with people = state.people @ [{Person.empty with id = x.id; name=x.name; }]}

let private ensurePersonExists (state:State) (x:Named) = 
  match state.person x.id with 
  | None -> 
    createPerson state x
  | Some _ -> state

let projectFutureState (state: State) (event: Timed<Event>) =
    match event.event with 
    | PersonCreated x -> 
        createPerson state x
    | PersonUpdated x -> 
        let stateWithPerson = ensurePersonExists state x.person
        let person = stateWithPerson.person x.person.id |> Option.get
        let updatedPerson = {
          person with 
            name=x.name |> Option.defaultValue person.name
            email=x.email |> Option.defaultValue person.email
            phone=x.phone |> Option.defaultValue person.phone
        }
        {stateWithPerson with people = stateWithPerson.people |> List.map (fun p -> if p.id=updatedPerson.id then updatedPerson else p )}

let projectPeople (src:EventSource<Event>) = 
  projectFromSource State.initial projectFutureState  src

type PersonDetailsParas = { person: Guid; name: string option ; email: string option; phone: string option }

type Command = 
  | AddPerson of string
  | UpdatePersonDetails of PersonDetailsParas


let processCommandOnState (state:State) (user: UserId) (cmd: Command) =
  match cmd with
  | AddPerson x ->
      let person = 
        x 
        |> Named.create
      printfn "Adding person %O" person
      person
      |> PersonCreated 
      |> Seq.singleton
      |> fun x -> {events=x; by=user}
      |> Success
  | UpdatePersonDetails x ->
      let name = x.name |> Option.defaultValue "name"
      let named = {Named.id=x.person; name=name}
      printfn "Updating person %O" x.person
      {| name=x.name; person=named; email=x.email; phone=x.phone; |}
      |> PersonUpdated 
      |> Seq.singleton
      |> fun x -> {events=x; by=user}
      |> Success

let private personCommands = {Commands.utcNow=UtcNow; logger=log; processor=processCommandOnState}

let processPeopleCommand (src:EventSource<Event>) (user:UserId) (cmd: Command) =
  let state = Projection.projectFromSource  State.initial projectFutureState src
  personCommands.applyCommandToEventSource state src user cmd