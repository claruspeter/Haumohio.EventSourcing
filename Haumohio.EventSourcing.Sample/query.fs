namespace Haumohio.EventSourcing.Sample

type Query() =
  member this.names = ["Jo"; "Alex"]

type Mutations() =
  member this.addName (name:string) = "<insert name into data and return the list of names>"