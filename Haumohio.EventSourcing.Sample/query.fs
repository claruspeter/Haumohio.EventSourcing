namespace Haumohio.EventSourcing.Sample



type Query() =
  member this.people = Domain.people()

type Mutations() =
  member this.addPerson (name:string) =
    Domain.addPerson name
    