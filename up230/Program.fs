open System.IO
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Haumohio.Storage

let private removeExistingStates (container:StorageContainer) domain =
  let states = 
    container.list domain
    |> Seq.filter (
      fun x -> 
        let fn = Path.GetFileName x
        fn.[0] >= 'A' && fn.[0] <= 'Z'
    )
    |> Seq.toList
  states
    |> Seq.iter (
      fun x -> 
        printfn "  - %s" (x.Substring(domain.Length + 1))
        container.remove x
    )
  printfn "  %d states removed" states.Length
  domain

let private v230Name domain (v220Name: string) = 
  let fname = Path.GetFileName v220Name
  let parts = fname.Split('_')
  sprintf "%s/event/%s_%s" domain (parts.[1]) (parts.[3])

let private moveEventsInDomain (container:StorageContainer) domain =
  printfn "%s:" domain
  let input = 
    container.list $"{domain}/"
    |> Seq.filter (fun x -> x |> Path.GetFileName |> _.StartsWith("event"))
  input 
    |> Seq.iter (
        fun oldName -> 
          printfn "  > %s" oldName
          let newName = v230Name domain oldName
          let contents = container.load oldName
          let saved = container.save newName contents.Value
          container.remove oldName
          printfn "  < %s" newName
      )
  let len = input |> Seq.length
  printfn "  %d events moved" len
  domain

let private moveEvents (container:StorageContainer) =
  printfn "\n%s" container.name
  printfn "%s" (String.init container.name.Length (fun _ -> "-"))
  container.part ""
  |> Seq.iter (moveEventsInDomain container >> removeExistingStates container >> ignore)



printfn "Upgrade stored event sourcing project to 2.3.0"
printfn "==============================================\n"

let logger = LoggerFactory.Create(fun x -> x.AddConsole() |> ignore).CreateLogger("up230")

let store = Files.FileStore logger (Some "../__data__")
printfn "Containers: "
let containers = store.containers() 

containers
|> Seq.map store.container
|> Seq.iter moveEvents
