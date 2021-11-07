namespace Haumohio.EventSourcing
open System
open System.IO
open Haumohio.EventSourcing.Common

type FileEventSource(domain) = 
  inherit  EventSource(domain)

  let ROOT_FOLDER = "/home/peter/Projects/misc/sportsball5/data/events/"

  member private this.loadFile<'Tevt> filename =
    try
      filename 
      |> File.ReadAllText
      |> Newtonsoft.Json.JsonConvert.DeserializeObject<Timed<'Tevt>>
    with 
    | exc ->
      Common.fail (fun u msg -> printfn "%s:%s (%s)" u msg filename) "LOADING" exc

  override this.loadFromDomain<'Tevt> domain =
    let folder = ROOT_FOLDER + domain
    if Directory.Exists(folder) |> not then Directory.CreateDirectory(folder) |> ignore
    Directory.EnumerateFiles(folder)
    |> Seq.sort
    |> Seq.map this.loadFile<'Tevt>

  override this.appendToDomain domain eventsGenerated=
    try
      let folder = ROOT_FOLDER + domain
      let datestamp (x: DateTime)= x.ToString("yyyy-MM-dd-HH-mm-ss-fff")
      eventsGenerated
      |> Seq.map (fun x -> {| name=DUName x.event; body=Newtonsoft.Json.JsonConvert.SerializeObject x; at=x.at |})
      |> Seq.iteri (fun i x -> File.WriteAllText ($"{folder}/{datestamp x.at}_{i}_{x.name}.json", x.body)) 
      this :> _
    with
    | exc -> fail (printfn "%s:%s") "evtsource" exc