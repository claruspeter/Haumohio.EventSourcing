namespace Haumohio.EventSourcing.Sample
open System 

module Domain =
  open System.Text.RegularExpressions
  open Microsoft.Extensions.Logging
  open HashidsNet
  open Haumohio.EventSourcing
  open Haumohio.EventSourcing.Projection
  open Haumohio.EventSourcing.EventStorage
  open Haumohio.Storage


  let private hasher salt=
    new Hashids(salt, minHashLength=8, alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ23456789")

  let calcId prefix clientId=
    let result = 
      hasher(clientId).Encode(DateTime.UtcNow.Ticks / 10000L |> int)
      |> fun x -> Regex.Replace(x, ".{4}", "$0-")
      |> fun x -> x.Remove( x.Length - 1)
    prefix + "-" + result 

  type DomainEvent =
    | PersonAdded of {| id: string; personalName:string; familyName: string |}
    | RoleAssigned of {| personId: string; roleName: string |}
    interface IHasDescription with
        member this.description: string = 
          match this with 
          | PersonAdded x -> x.id
          | RoleAssigned x -> x.roleName


  type Person = {
    id: string
    personalName: string;
    familyName: string;
    roles: string Set
  }with 
    interface IHasKey<string> with 
      member this.Key = this.id
    interface IEmpty<Person> with 
      static member empty = {id=""; personalName=""; familyName=""; roles=set []}
    interface IAutoClean<Person> with 
      member this.clean (): Person = this

  let private empty = State<string, Person>.empty

  let EventStore c = 
    {
      eventContainer=c
      stateContainer=c
    }
  
  let store = Memory.MemoryStore
  // let store = Files.FileStore Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance (Some "/home/peter/Projects/misc/Haumohio.EventSourcingV2/__data_files__")
  let private container clientId = store.container  clientId |> EventStore

  let projector (state: State<string,Person>) event =
    match event.details with 
    | PersonAdded x -> addOrAmend x.id (fun person -> {person with id = x.id; personalName = x.personalName; familyName = x.familyName; roles = set [] }) state
    | RoleAssigned x -> amend x.personId (fun person -> {person with roles = person.roles |> Set.add x.roleName }) state

  type SampleDomains = 
    | People = 1


  let people clientId  =
    let c = clientId |> container 
    StateStorage.makeState SampleDomains.People projector c
    |> fun x -> x.data.Values

  let addPerson clientId userName personalName familyName =
    let c = clientId |> container
    let eventDetail = {| id=calcId "P" clientId; personalName=personalName; familyName=familyName |}
    eventDetail
    |> PersonAdded
    |> storeEvent  c SampleDomains.People userName 

  let assignRole clientId userName personId roleName =
    let c = clientId |> container
    RoleAssigned {| personId = personId; roleName = roleName |}
    |> storeEvent c SampleDomains.People userName 
